use std::path::PathBuf;
use rusqlite::Connection;
use tempfile::tempdir;
use xtr_gemini::{xml, db};
use serde_json::Value;
#[derive(Debug, PartialEq)]
struct RowCmp {
  edid: Option<String>,
  rec: Option<String>,
  src: String,
  dst: Option<String>,
  meta: Value,
}
fn read_rows(conn: &Connection) -> Vec<RowCmp> {
  let mut st = conn.prepare("SELECT edid, rec, src, dst, meta_json FROM entries ORDER BY id").unwrap();
  let rows = st.query_map([], |r| {
    let meta_str: Option<String> = r.get(4)?;
    let meta_v: Value = meta_str
      .and_then(|s| serde_json::from_str(&s).ok())
      .unwrap_or(Value::Null);
    Ok(RowCmp {
      edid: r.get(0)?,
      rec: r.get(1)?,
      src: r.get(2)?,
      dst: r.get(3)?,
      meta: meta_v,
    })
  }).unwrap();
  let mut out = Vec::new();
  for r in rows { out.push(r.unwrap()); }
  out
}
#[test]
fn xml_db_xml_roundtrip_preserves_count_and_params() {
  // Resolve sample XML path (repo root)
  let manifest = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
  let sample = manifest.parent().unwrap().parent().unwrap().join("LegacyoftheDragonborn_english_korean.xml");
  assert!(sample.exists(), "sample xml not found at {}", sample.display());
  // temp dir + db
  let dir = tempdir().unwrap();
  let db_path = dir.path().join("xtr.db");
  let conn = Connection::open(&db_path).unwrap();
  db::init_on_connection(&conn).unwrap();
  // parse input into db
  let (count_in, params_json) = xml::parse_into_conn(&conn, sample.to_str().unwrap()).unwrap();
  assert!(count_in > 0, "parsed zero strings from sample");
  let rows1 = read_rows(&conn);
  // write out xml
  let out_path = dir.path().join("out.xml");
  let count_out = xml::write_from_conn(&conn, &params_json, &out_path).unwrap();
  assert_eq!(count_in, count_out, "string count mismatch");
  assert!(out_path.exists(), "out.xml not created");
  // parse written xml into a second db and compare counts again
  let dir2 = tempdir().unwrap();
  let db_path2 = dir2.path().join("xtr2.db");
  let conn2 = Connection::open(&db_path2).unwrap();
  db::init_on_connection(&conn2).unwrap();
  let (count_in2, params_json2) = xml::parse_into_conn(&conn2, out_path.to_str().unwrap()).unwrap();
  assert_eq!(count_in, count_in2, "roundtrip parse count mismatch");
  // params presence
  assert!(params_json.get("Addon").is_some(), "missing Addon param");
  assert!(params_json2.get("Addon").is_some(), "missing Addon in roundtrip");
  // rows structural equality (ignoring updated_at/status)
  let rows2 = read_rows(&conn2);
  assert_eq!(rows1.len(), rows2.len(), "row len mismatch");
  for (i, (a, b)) in rows1.iter().zip(rows2.iter()).enumerate() {
    assert_eq!(a, b, "row {} mismatch", i);
  }
}
#[test]
fn xml_roundtrip_edge_cases_params_and_attributes() {
  // Build synthetic XML with edge cases
  let xml_str = r#"<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<SSTXMLRessources>
  <Params>
    <Addon>Test.esm</Addon>
    <Source>english</Source>
    <Dest>korean</Dest>
    <Version>2</Version>
  </Params>
  <Content>
    <String List="0" Partial="1">
      <EDID>E1</EDID>
      <REC id="1" idMax="9">BOOK:FULL</REC>
      <Source>Hello, {name}! %d $1</Source>
      <Dest></Dest>
    </String>
    <String List="1" Partial="0">
      <EDID>E2</EDID>
      <REC>DESC</REC>
      <Source>&lt;b&gt;A &lt;i&gt;very&lt;/i&gt; bold&lt;/b&gt; text</Source>
      <Dest>&lt;b&gt;A &lt;i&gt;very&lt;/i&gt; bold&lt;/b&gt; text</Dest>
    </String>
    <String List="0" Partial="1">
      <EDID>E3</EDID>
      <REC>BOOK:DESC</REC>
      <Source>{itemCount, plural, one{1 item} other{# items}}</Source>
      <Dest></Dest>
    </String>
  </Content>
</SSTXMLRessources>
"#;
  let dir = tempdir().unwrap();
  let in_path = dir.path().join("in.xml");
  std::fs::write(&in_path, xml_str).unwrap();
  let db_path = dir.path().join("xtr.db");
  let conn = Connection::open(&db_path).unwrap();
  db::init_on_connection(&conn).unwrap();
  let (cnt1, params1) = xml::parse_into_conn(&conn, in_path.to_str().unwrap()).unwrap();
  assert_eq!(cnt1, 3, "expected 3 strings");
  // Check params equality against expected
  assert_eq!(params1.get("Addon").and_then(|v| v.as_str()), Some("Test.esm"));
  assert_eq!(params1.get("Source").and_then(|v| v.as_str()), Some("english"));
  assert_eq!(params1.get("Dest").and_then(|v| v.as_str()), Some("korean"));
  assert_eq!(params1.get("Version").and_then(|v| v.as_str()), Some("2"));
  // Write out and re-parse
  let out_path = dir.path().join("out.xml");
  let cnt_out = xml::write_from_conn(&conn, &params1, &out_path).unwrap();
  assert_eq!(cnt1, cnt_out, "write count mismatch");
  let db2 = Connection::open(dir.path().join("xtr2.db")).unwrap();
  db::init_on_connection(&db2).unwrap();
  let (cnt2, params2) = xml::parse_into_conn(&db2, out_path.to_str().unwrap()).unwrap();
  assert_eq!(cnt1, cnt2, "roundtrip count mismatch");
  assert_eq!(params1, params2, "params mismatch after roundtrip");
  // Compare rows including attributes snapshot in meta_json
  let rows1 = read_rows(&conn);
  let rows2 = read_rows(&db2);
  assert_eq!(rows1, rows2, "row structures differ after roundtrip");
  // Specific checks
  // E1 has empty Dest => None
  let e1 = rows2.iter().find(|r| r.edid.as_deref() == Some("E1")).unwrap();
  assert!(e1.dst.is_none(), "E1 dst should be None (empty)");
  // E2 has HTML-escaped nested tags preserved
  let e2 = rows2.iter().find(|r| r.edid.as_deref() == Some("E2")).unwrap();
  assert!(e2.src.contains("&lt;b&gt;A"));
  assert_eq!(e2.dst.as_deref(), Some("&lt;b&gt;A &lt;i&gt;very&lt;/i&gt; bold&lt;/b&gt; text"));
  // E3 has ICU placeholder preserved
  let e3 = rows2.iter().find(|r| r.edid.as_deref() == Some("E3")).unwrap();
  assert!(e3.src.contains("{itemCount, plural"));
  // Attributes preserved in meta_json
  let meta = &e1.meta;
  assert_eq!(meta["string_attrs"]["List"].as_str(), Some("0"));
  assert_eq!(meta["string_attrs"]["Partial"].as_str(), Some("1"));
  assert_eq!(meta["rec_attrs"]["id"].as_str(), Some("1"));
  assert_eq!(meta["rec_attrs"]["idMax"].as_str(), Some("9"));
}
