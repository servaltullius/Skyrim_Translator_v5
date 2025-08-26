use anyhow::{Result, Context};
use tauri::{AppHandle, Manager};
use rusqlite::{params, Connection, Statement};
use std::fs::File;
use std::io::{BufReader, Write};
use std::path::Path;
use std::collections::HashMap;
use quick_xml::events::{Event, BytesStart, BytesEnd, BytesText};
use quick_xml::{Reader, Writer};
fn commit_row_stmt(
  insert: &mut Statement,
  cur_edid: &mut String,
  cur_rec: &mut String,
  cur_src: &mut String,
  cur_dst: &mut String,
  string_attrs: &mut HashMap<String, String>,
  rec_attrs: &mut HashMap<String, String>,
) -> Result<()> {
  if cur_src.is_empty() && cur_dst.is_empty() && cur_edid.is_empty() && cur_rec.is_empty() {
    return Ok(());
  }
  let status = if !cur_dst.trim().is_empty() { "done" } else { "pending" };
  let meta = serde_json::json!({
    "string_attrs": string_attrs.clone(),
    "rec_attrs": rec_attrs.clone()
  }).to_string();
  let edid_opt = if cur_edid.is_empty() { None } else { Some(cur_edid.clone()) };
  let rec_opt = if cur_rec.is_empty() { None } else { Some(cur_rec.clone()) };
  let dst_opt = if cur_dst.is_empty() { None } else { Some(cur_dst.clone()) };
  insert.execute(params![edid_opt, rec_opt, cur_src.clone(), dst_opt, status, meta])?;
  // reset
  cur_edid.clear();
  cur_rec.clear();
  *cur_src = String::new();
  *cur_dst = String::new();
  string_attrs.clear();
  rec_attrs.clear();
  Ok(())
}
/// Load xTranslator XML via streaming parse into SQLite.
pub async fn load_xtranslator_xml(app: &AppHandle, path: &str) -> Result<usize> {
  crate::db::init(app)?;
  crate::db::reset_entries(app)?;
  // Parse XML
  let file = File::open(path).with_context(|| format!("Failed to open XML: {}", path))?;
  let reader = BufReader::new(file);
  let mut reader = Reader::from_reader(reader);
  reader.trim_text(true);
  let mut buf = Vec::new();
  // Params collection
  let mut in_params = false;
  let mut cur_param_key: Option<String> = None;
  let mut params_map: HashMap<String, String> = HashMap::new();
  // Content/String collection
  let mut in_string = false;
  let mut cur_tag: Option<&'static str> = None; // "EDID" | "REC" | "Source" | "Dest"
  let mut cur_edid = String::new();
  let mut cur_rec = String::new();
  let mut cur_src = String::new();
  let mut cur_dst = String::new();
  let mut string_attrs: HashMap<String, String> = HashMap::new(); // List, Partial
  let mut rec_attrs: HashMap<String, String> = HashMap::new(); // id, idMax
  // DB transaction + prepared insert
  let mut conn = Connection::open(app.path().app_local_data_dir()?.join("xtr.db"))?;
  let tx = conn.unchecked_transaction()?;
  let mut insert = tx.prepare(
    "INSERT INTO entries(edid, rec, src, dst, status, meta_json)
     VALUES(?1, ?2, ?3, ?4, ?5, ?6)")?;
  loop {
    match reader.read_event_into(&mut buf) {
      Ok(Event::Start(e)) => {
        let name_vec = e.name().as_ref().to_vec();
        match name_vec.as_slice() {
          b"Params" => in_params = true,
          b"Addon" | b"Source" | b"Dest" | b"Version" if in_params => {
            cur_param_key = Some(std::str::from_utf8(&name_vec).unwrap().to_string());
          }
          b"Content" => { /* enter content */ }
          b"String" => {
            in_string = true;
            string_attrs.clear();
            // capture attributes like List, Partial
            for a in e.attributes().with_checks(false) {
              if let Ok(a) = a {
                let key = std::str::from_utf8(a.key.as_ref()).unwrap_or_default().to_string();
                let val = a.unescape_value().unwrap_or_default().to_string();
                string_attrs.insert(key, val);
              }
            }
          }
          b"EDID" if in_string => { cur_tag = Some("EDID"); }
          b"REC" if in_string => {
            cur_tag = Some("REC");
            rec_attrs.clear();
            for a in e.attributes().with_checks(false) {
              if let Ok(a) = a {
                let key = std::str::from_utf8(a.key.as_ref()).unwrap_or_default().to_string();
                let val = a.unescape_value().unwrap_or_default().to_string();
                rec_attrs.insert(key, val);
              }
            }
          }
          b"Source" if in_string => { cur_tag = Some("Source"); }
          b"Dest" if in_string => { cur_tag = Some("Dest"); }
          _ => {}
        }
      }
      Ok(Event::Text(t)) => {
        let text = t.unescape().unwrap_or_default().to_string();
        if in_params {
          if let Some(ref k) = cur_param_key {
            params_map.insert(k.clone(), text);
            cur_param_key = None;
          }
        } else if in_string {
          match cur_tag {
            Some("EDID") => cur_edid.push_str(&text),
            Some("REC") => cur_rec.push_str(&text),
            Some("Source") => {
              if !cur_src.is_empty() { cur_src.push_str(" "); }
              cur_src.push_str(&text);
            }
            Some("Dest") => {
              if !cur_dst.is_empty() { cur_dst.push_str(" "); }
              cur_dst.push_str(&text);
            }
            _ => {}
          }
        }
      }
      Ok(Event::CData(t)) => {
        // treat as raw text content (rare for these files)
        let text = String::from_utf8_lossy(&t).to_string();
        if in_string {
          match cur_tag {
            Some("Source") => cur_src.push_str(&text),
            Some("Dest") => cur_dst.push_str(&text),
            _ => {}
          }
        }
      }
      Ok(Event::End(e)) => {
        let name_vec = e.name().as_ref().to_vec();
        match name_vec.as_slice() {
          b"Params" => in_params = false,
          b"String" => {
            in_string = false;
            cur_tag = None;
            commit_row_stmt(&mut insert, &mut cur_edid, &mut cur_rec, &mut cur_src, &mut cur_dst, &mut string_attrs, &mut rec_attrs)?;
          }
          b"EDID" | b"REC" | b"Source" | b"Dest" => { cur_tag = None; }
          _ => {}
        }
      }
      Ok(Event::Eof) => break,
      Err(e) => return Err(anyhow::anyhow!("XML parse error: {}", e)),
      _ => {}
    }
    buf.clear();
  }
  drop(insert);
  tx.commit()?;
  // store params + source path in settings
  let mut patch = serde_json::Map::new();
  patch.insert("sourcePath".into(), serde_json::Value::String(path.to_string()));
  patch.insert("xml_params".into(), serde_json::to_value(&params_map).unwrap_or(serde_json::Value::Null));
  crate::db::update_settings(app, serde_json::Value::Object(patch))?;
  let count = crate::db::count_entries(app)?;
  Ok(count as usize)
}
/// Write DB entries back to xTranslator XML at target path.
pub async fn write_xtranslator_xml(app: &AppHandle, target_path: Option<&str>) -> Result<usize> {
  crate::db::init(app)?;
  let out_path = if let Some(p) = target_path {
    std::path::PathBuf::from(p)
  } else {
    app.path().app_local_data_dir()?.join("out.xml")
  };
  // load settings params
  let settings = crate::db::get_settings(app)?;
  let xml_params = settings.get("xml_params").cloned().unwrap_or(serde_json::json!({}));
  let addon = xml_params.get("Addon").and_then(|v| v.as_str()).unwrap_or_default();
  let src_lang = xml_params.get("Source").and_then(|v| v.as_str()).unwrap_or("english");
  let dst_lang = xml_params.get("Dest").and_then(|v| v.as_str()).unwrap_or("korean");
  let ver = xml_params.get("Version").and_then(|v| v.as_str()).unwrap_or("2");
  // fetch entries
  let conn = Connection::open(app.path().app_local_data_dir()?.join("xtr.db"))?;
  let mut st = conn.prepare("SELECT id, edid, rec, src, dst, meta_json FROM entries ORDER BY id")?;
  let rows = st.query_map([], |r| {
    let meta: Option<String> = r.get(5)?;
    Ok((
      r.get::<_, i64>(0)?,
      r.get::<_, Option<String>>(1)?,
      r.get::<_, Option<String>>(2)?,
      r.get::<_, String>(3)?,
      r.get::<_, Option<String>>(4)?,
      meta,
    ))
  })?;
  // writer
  let mut w = Writer::new_with_indent(Vec::new(), b' ', 2);
  // header
  w.write_event(Event::Decl(quick_xml::events::BytesDecl::new("1.0", Some("UTF-8"), Some("yes"))))?;
  // <SSTXMLRessources>
  w.write_event(Event::Start(BytesStart::new("SSTXMLRessources")))?;
  // <Params>
  w.write_event(Event::Start(BytesStart::new("Params")))?;
  // Addon
  write_simple(&mut w, "Addon", addon)?;
  write_simple(&mut w, "Source", src_lang)?;
  write_simple(&mut w, "Dest", dst_lang)?;
  write_simple(&mut w, "Version", ver)?;
  w.write_event(Event::End(BytesEnd::new("Params")))?;
  // <Content>
  w.write_event(Event::Start(BytesStart::new("Content")))?;
  let mut count = 0usize;
  for row in rows {
    let (_id, edid, rec, src, dst, meta) = row?;
    // parse meta for attributes
    let mut list_attr: Option<String> = None;
    let mut partial_attr: Option<String> = None;
    let mut rec_id: Option<String> = None;
    let mut rec_id_max: Option<String> = None;
    if let Some(meta_str) = meta {
      if let Ok(v) = serde_json::from_str::<serde_json::Value>(&meta_str) {
        if let Some(sa) = v.get("string_attrs") {
          list_attr = sa.get("List").and_then(|x| x.as_str()).map(|s| s.to_string());
          partial_attr = sa.get("Partial").and_then(|x| x.as_str()).map(|s| s.to_string());
        }
        if let Some(ra) = v.get("rec_attrs") {
          rec_id = ra.get("id").and_then(|x| x.as_str()).map(|s| s.to_string());
          rec_id_max = ra.get("idMax").and_then(|x| x.as_str()).map(|s| s.to_string());
        }
      }
    }
    // <String ...>
    let mut el = BytesStart::new("String");
    if let Some(v) = list_attr.as_deref() { el.push_attribute(("List", v)); }
    if let Some(v) = partial_attr.as_deref() { el.push_attribute(("Partial", v)); }
    w.write_event(Event::Start(el))?;
    // EDID
    if let Some(e) = edid.as_deref() { write_simple(&mut w, "EDID", e)?; }
    // REC
    if let Some(rec_text) = rec.as_deref() {
      let mut rec_el = BytesStart::new("REC");
      if let Some(v) = rec_id.as_deref() { rec_el.push_attribute(("id", v)); }
      if let Some(v) = rec_id_max.as_deref() { rec_el.push_attribute(("idMax", v)); }
      w.write_event(Event::Start(rec_el))?;
      w.write_event(Event::Text(BytesText::new(rec_text)))?;
      w.write_event(Event::End(BytesEnd::new("REC")))?;
    }
    // Source
    write_simple(&mut w, "Source", &src)?;
    // Dest
    write_simple(&mut w, "Dest", dst.as_deref().unwrap_or(""))?;
    // </String>
    w.write_event(Event::End(BytesEnd::new("String")))?;
    count += 1;
  }
  // </Content></SSTXMLRessources>
  w.write_event(Event::End(BytesEnd::new("Content")))?;
  w.write_event(Event::End(BytesEnd::new("SSTXMLRessources")))?;
  let xml_bytes = w.into_inner();
  let mut f = File::create(&out_path).with_context(|| format!("Failed to create {}", out_path.display()))?;
  f.write_all(&xml_bytes)?;
  Ok(count)
}
fn write_simple(w: &mut Writer<Vec<u8>>, name: &str, text: &str) -> Result<()> {
  w.write_event(Event::Start(BytesStart::new(name)))?;
  w.write_event(Event::Text(BytesText::new(text)))?;
  w.write_event(Event::End(BytesEnd::new(name)))?;
  Ok(())
}
/// Testable helpers: parse into an existing connection and return count + params json.
pub fn parse_into_conn(conn: &Connection, path: &str) -> Result<(usize, serde_json::Value)> {
  conn.execute("DELETE FROM entries", [])?;
  let file = File::open(path).with_context(|| format!("Failed to open XML: {}", path))?;
  let reader = BufReader::new(file);
  let mut reader = Reader::from_reader(reader);
  reader.trim_text(true);
  let mut buf = Vec::new();
  let mut in_params = false;
  let mut cur_param_key: Option<String> = None;
  let mut params_map: HashMap<String, String> = HashMap::new();
  let mut in_string = false;
  let mut cur_tag: Option<&'static str> = None;
  let mut cur_edid = String::new();
  let mut cur_rec = String::new();
  let mut cur_src = String::new();
  let mut cur_dst = String::new();
  let mut string_attrs: HashMap<String, String> = HashMap::new();
  let mut rec_attrs: HashMap<String, String> = HashMap::new();
  let tx = conn.unchecked_transaction()?;
  let mut insert = tx.prepare(
    "INSERT INTO entries(edid, rec, src, dst, status, meta_json)
     VALUES(?1, ?2, ?3, ?4, ?5, ?6)")?;
  let mut count = 0usize;
  loop {
    match reader.read_event_into(&mut buf) {
      Ok(Event::Start(e)) => {
        let name_vec = e.name().as_ref().to_vec();
        match name_vec.as_slice() {
          b"Params" => in_params = true,
          b"Addon" | b"Source" | b"Dest" | b"Version" if in_params => {
            cur_param_key = Some(std::str::from_utf8(&name_vec).unwrap().to_string());
          }
          b"String" => {
            in_string = true;
            string_attrs.clear();
            for a in e.attributes().with_checks(false) {
              if let Ok(a) = a {
                let key = std::str::from_utf8(a.key.as_ref()).unwrap_or_default().to_string();
                let val = a.unescape_value().unwrap_or_default().to_string();
                string_attrs.insert(key, val);
              }
            }
          }
          b"EDID" if in_string => { cur_tag = Some("EDID"); }
          b"REC" if in_string => {
            cur_tag = Some("REC");
            rec_attrs.clear();
            for a in e.attributes().with_checks(false) {
              if let Ok(a) = a {
                let key = std::str::from_utf8(a.key.as_ref()).unwrap_or_default().to_string();
                let val = a.unescape_value().unwrap_or_default().to_string();
                rec_attrs.insert(key, val);
              }
            }
          }
          b"Source" if in_string => { cur_tag = Some("Source"); }
          b"Dest" if in_string => { cur_tag = Some("Dest"); }
          _ => {}
        }
      }
      Ok(Event::Text(t)) => {
        let text = t.unescape().unwrap_or_default().to_string();
        if in_params {
          if let Some(ref k) = cur_param_key {
            params_map.insert(k.clone(), text);
            cur_param_key = None;
          }
        } else if in_string {
          match cur_tag {
            Some("EDID") => cur_edid.push_str(&text),
            Some("REC") => cur_rec.push_str(&text),
            Some("Source") => {
              if !cur_src.is_empty() { cur_src.push_str(" "); }
              cur_src.push_str(&text);
            }
            Some("Dest") => {
              if !cur_dst.is_empty() { cur_dst.push_str(" "); }
              cur_dst.push_str(&text);
            }
            _ => {}
          }
        }
      }
      Ok(Event::CData(t)) => {
        let text = String::from_utf8_lossy(&t).to_string();
        if in_string {
          match cur_tag {
            Some("Source") => cur_src.push_str(&text),
            Some("Dest") => cur_dst.push_str(&text),
            _ => {}
          }
        }
      }
      Ok(Event::End(e)) => {
        let name_vec = e.name().as_ref().to_vec();
        match name_vec.as_slice() {
          b"Params" => in_params = false,
          b"String" => {
            in_string = false;
            cur_tag = None;
            commit_row_stmt(&mut insert, &mut cur_edid, &mut cur_rec, &mut cur_src, &mut cur_dst, &mut string_attrs, &mut rec_attrs)?;
            count += 1;
          }
          b"EDID" | b"REC" | b"Source" | b"Dest" => { cur_tag = None; }
          _ => {}
        }
      }
      Ok(Event::Eof) => break,
      Err(e) => return Err(anyhow::anyhow!("XML parse error: {}", e)),
      _ => {}
    }
    buf.clear();
  }
  drop(insert);
  tx.commit()?;
  Ok((count, serde_json::to_value(params_map).unwrap_or(serde_json::json!({}))))
}
pub fn write_from_conn(conn: &Connection, xml_params: &serde_json::Value, out_path: &Path) -> Result<usize> {
  let addon = xml_params.get("Addon").and_then(|v| v.as_str()).unwrap_or_default();
  let src_lang = xml_params.get("Source").and_then(|v| v.as_str()).unwrap_or("english");
  let dst_lang = xml_params.get("Dest").and_then(|v| v.as_str()).unwrap_or("korean");
  let ver = xml_params.get("Version").and_then(|v| v.as_str()).unwrap_or("2");
  let mut st = conn.prepare("SELECT id, edid, rec, src, dst, meta_json FROM entries ORDER BY id")?;
  let rows = st.query_map([], |r| {
    let meta: Option<String> = r.get(5)?;
    Ok((
      r.get::<_, i64>(0)?,
      r.get::<_, Option<String>>(1)?,
      r.get::<_, Option<String>>(2)?,
      r.get::<_, String>(3)?,
      r.get::<_, Option<String>>(4)?,
      meta,
    ))
  })?;
  let mut w = Writer::new_with_indent(Vec::new(), b' ', 2);
  w.write_event(Event::Decl(quick_xml::events::BytesDecl::new("1.0", Some("UTF-8"), Some("yes"))))?;
  w.write_event(Event::Start(BytesStart::new("SSTXMLRessources")))?;
  w.write_event(Event::Start(BytesStart::new("Params")))?;
  write_simple(&mut w, "Addon", addon)?;
  write_simple(&mut w, "Source", src_lang)?;
  write_simple(&mut w, "Dest", dst_lang)?;
  write_simple(&mut w, "Version", ver)?;
  w.write_event(Event::End(BytesEnd::new("Params")))?;
  w.write_event(Event::Start(BytesStart::new("Content")))?;
  let mut count = 0usize;
  for row in rows {
    let (_id, edid, rec, src, dst, meta) = row?;
    let mut list_attr: Option<String> = None;
    let mut partial_attr: Option<String> = None;
    let mut rec_id: Option<String> = None;
    let mut rec_id_max: Option<String> = None;
    if let Some(meta_str) = meta {
      if let Ok(v) = serde_json::from_str::<serde_json::Value>(&meta_str) {
        if let Some(sa) = v.get("string_attrs") {
          list_attr = sa.get("List").and_then(|x| x.as_str()).map(|s| s.to_string());
          partial_attr = sa.get("Partial").and_then(|x| x.as_str()).map(|s| s.to_string());
        }
        if let Some(ra) = v.get("rec_attrs") {
          rec_id = ra.get("id").and_then(|x| x.as_str()).map(|s| s.to_string());
          rec_id_max = ra.get("idMax").and_then(|x| x.as_str()).map(|s| s.to_string());
        }
      }
    }
    let mut el = BytesStart::new("String");
    if let Some(v) = list_attr.as_deref() { el.push_attribute(("List", v)); }
    if let Some(v) = partial_attr.as_deref() { el.push_attribute(("Partial", v)); }
    w.write_event(Event::Start(el))?;
    if let Some(e) = edid.as_deref() { write_simple(&mut w, "EDID", e)?; }
    if let Some(rec_text) = rec.as_deref() {
      let mut rec_el = BytesStart::new("REC");
      if let Some(v) = rec_id.as_deref() { rec_el.push_attribute(("id", v)); }
      if let Some(v) = rec_id_max.as_deref() { rec_el.push_attribute(("idMax", v)); }
      w.write_event(Event::Start(rec_el))?;
      w.write_event(Event::Text(BytesText::new(rec_text)))?;
      w.write_event(Event::End(BytesEnd::new("REC")))?;
    }
    write_simple(&mut w, "Source", &src)?;
    write_simple(&mut w, "Dest", dst.as_deref().unwrap_or(""))?;
    w.write_event(Event::End(BytesEnd::new("String")))?;
    count += 1;
  }
  w.write_event(Event::End(BytesEnd::new("Content")))?;
  w.write_event(Event::End(BytesEnd::new("SSTXMLRessources")))?;
  let xml_bytes = w.into_inner();
  let mut f = File::create(out_path).with_context(|| format!("Failed to create {}", out_path.display()))?;
  f.write_all(&xml_bytes)?;
  Ok(count)
}
