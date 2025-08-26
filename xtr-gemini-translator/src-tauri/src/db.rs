use anyhow::{Result, anyhow};
use rusqlite::{Connection, params};
use serde_json::json;
use tauri::{AppHandle, Manager};
#[derive(Debug, Clone)]
pub struct Entry {
  pub id: i64,
  pub edid: Option<String>,
  pub rec: Option<String>,
  pub src: String,
}
pub struct ListRow {
  pub id: i64,
  pub status: String,
  pub src: String,
  pub dst: Option<String>,
}
pub fn init(app: &AppHandle) -> Result<()> {
  let conn = conn(app)?;
  conn.execute_batch(r#"
    PRAGMA journal_mode=WAL;
    CREATE TABLE IF NOT EXISTS entries (
      id INTEGER PRIMARY KEY,
      edid TEXT,
      rec  TEXT,
      src  TEXT NOT NULL,
      dst  TEXT,
      status TEXT NOT NULL DEFAULT 'pending',
      meta_json TEXT,
      updated_at TEXT DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now'))
    );
    CREATE INDEX IF NOT EXISTS idx_entries_status ON entries(status);
    CREATE TABLE IF NOT EXISTS glossary (
      key TEXT PRIMARY KEY,
      value TEXT NOT NULL,
      note TEXT
    );
    CREATE TABLE IF NOT EXISTS settings (
      key TEXT PRIMARY KEY,
      value TEXT NOT NULL
    );
  "#)?;
  Ok(())
}
pub fn init_on_connection(conn: &Connection) -> Result<()> {
  conn.execute_batch(r#"
    PRAGMA journal_mode=WAL;
    CREATE TABLE IF NOT EXISTS entries (
      id INTEGER PRIMARY KEY,
      edid TEXT,
      rec  TEXT,
      src  TEXT NOT NULL,
      dst  TEXT,
      status TEXT NOT NULL DEFAULT 'pending',
      meta_json TEXT,
      updated_at TEXT DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now'))
    );
    CREATE INDEX IF NOT EXISTS idx_entries_status ON entries(status);
    CREATE TABLE IF NOT EXISTS glossary (
      key TEXT PRIMARY KEY,
      value TEXT NOT NULL,
      note TEXT
    );
    CREATE TABLE IF NOT EXISTS settings (
      key TEXT PRIMARY KEY,
      value TEXT NOT NULL
    );
  "#)?;
  Ok(())
}
fn conn(app: &AppHandle) -> Result<Connection> {
  let db_path = app.path().app_local_data_dir()?.join("xtr.db");
  Ok(Connection::open(db_path)?)
}
pub fn get_glossary(app: &AppHandle) -> Result<serde_json::Value> {
  let c = conn(app)?;
  let mut st = c.prepare("SELECT key, value, note FROM glossary ORDER BY key")?;
  let rows = st.query_map([], |r| {
    Ok(json!({
      "key": r.get::<_, String>(0)?,
      "value": r.get::<_, String>(1)?,
      "note": r.get::<_, Option<String>>(2)?
    }))
  })?;
  let mut out = Vec::new();
  for r in rows { out.push(r?); }
  Ok(serde_json::Value::Array(out))
}
pub fn upsert_glossary(app: &AppHandle, json_val: serde_json::Value) -> Result<()> {
  let c = conn(app)?;
  let tx = c.unchecked_transaction()?;
  if let Some(arr) = json_val.as_array() {
    tx.execute("DELETE FROM glossary", [])?;
    for it in arr {
      let key = it.get("key").and_then(|v| v.as_str()).unwrap_or_default();
      let value = it.get("value").and_then(|v| v.as_str()).unwrap_or_default();
      let note = it.get("note").and_then(|v| v.as_str());
      tx.execute("INSERT OR REPLACE INTO glossary(key,value,note) VALUES(?,?,?)",
        params![key, value, note])?;
    }
  }
  tx.commit()?;
  Ok(())
}
pub fn get_settings(app: &AppHandle) -> Result<serde_json::Value> {
  let c = conn(app)?;
  let mut st = c.prepare("SELECT key, value FROM settings")?;
  let rows = st.query_map([], |r| {
    Ok((r.get::<_, String>(0)?, r.get::<_, String>(1)?))
  })?;
  let mut out = serde_json::Map::new();
  for r in rows {
    let (k, v) = r?;
    out.insert(k, serde_json::from_str(&v).unwrap_or(serde_json::Value::String(v)));
  }
  Ok(serde_json::Value::Object(out))
}
pub fn update_settings(app: &AppHandle, patch: serde_json::Value) -> Result<()> {
  let c = conn(app)?;
  let tx = c.unchecked_transaction()?;
  if let Some(obj) = patch.as_object() {
    for (k, v) in obj {
      tx.execute("INSERT OR REPLACE INTO settings(key,value) VALUES(?,?)",
        params![k, serde_json::to_string(v)?])?;
    }
  }
  tx.commit()?;
  Ok(())
}
pub fn select_pending(app: &AppHandle, limit: usize) -> Result<Vec<Entry>> {
  let c = conn(app)?;
  let mut st = c.prepare("SELECT id, edid, rec, src FROM entries WHERE status='pending' LIMIT ?1")?;
  let rows = st.query_map(params![limit as i64], |r| {
    Ok(Entry {
      id: r.get::<_, i64>(0)?,
      edid: r.get::<_, Option<String>>(1)?,
      rec: r.get::<_, Option<String>>(2)?,
      src: r.get::<_, String>(3)?,
    })
  })?;
  let mut out = Vec::new();
  for r in rows { out.push(r?); }
  Ok(out)
}
pub fn update_entry_done(app: &AppHandle, id: i64, dst: &str) -> Result<()> {
  let c = conn(app)?;
  c.execute(
    "UPDATE entries SET dst=?1, status='done', updated_at=(strftime('%Y-%m-%dT%H:%M:%fZ','now')) WHERE id=?2",
    params![dst, id],
  )?;
  Ok(())
}
pub fn update_entry_done_with_conn(conn: &Connection, id: i64, dst: &str) -> Result<()> {
  conn.execute(
    "UPDATE entries SET dst=?1, status='done', updated_at=(strftime('%Y-%m-%dT%H:%M:%fZ','now')) WHERE id=?2",
    params![dst, id],
  )?;
  Ok(())
}
pub fn clear_entry_dst(app: &AppHandle, id: i64) -> Result<()> {
  let c = conn(app)?;
  c.execute(
    "UPDATE entries SET dst=NULL, status='pending', updated_at=(strftime('%Y-%m-%dT%H:%M:%fZ','now')) WHERE id=?1",
    params![id],
  )?;
  Ok(())
}
pub fn update_entry_error(app: &AppHandle, id: i64, code: &str) -> Result<()> {
  let c = conn(app)?;
  let meta = json!({"error": code});
  c.execute(
    "UPDATE entries SET status='error', meta_json=?1, updated_at=(strftime('%Y-%m-%dT%H:%M:%fZ','now')) WHERE id=?2",
    params![meta.to_string(), id],
  )?;
  Ok(())
}
pub fn update_entry_error_ex(app: &AppHandle, id: i64, code: &str, message: Option<&str>) -> Result<()> {
  let c = conn(app)?;
  let meta = json!({"error": code, "message": message});
  c.execute(
    "UPDATE entries SET status='error', meta_json=?1, updated_at=(strftime('%Y-%m-%dT%H:%M:%fZ','now')) WHERE id=?2",
    params![meta.to_string(), id],
  )?;
  Ok(())
}
pub fn reset_entries(app: &AppHandle) -> Result<()> {
  let c = conn(app)?;
  c.execute("DELETE FROM entries", [])?;
  Ok(())
}
pub fn count_entries(app: &AppHandle) -> Result<i64> {
  let c = conn(app)?;
  let mut st = c.prepare("SELECT COUNT(*) FROM entries")?;
  let mut rows = st.query([])?;
  let cnt: i64 = if let Some(row) = rows.next()? { row.get(0)? } else { 0 };
  Ok(cnt)
}
pub fn list_entries(app: &AppHandle, limit: usize, offset: usize) -> Result<Vec<ListRow>> {
  let c = conn(app)?;
  let mut st = c.prepare("SELECT id, status, src, dst FROM entries ORDER BY id LIMIT ?1 OFFSET ?2")?;
  let rows = st.query_map(params![limit as i64, offset as i64], |r| {
    Ok(ListRow {
      id: r.get::<_, i64>(0)?,
      status: r.get::<_, String>(1)?,
      src: r.get::<_, String>(2)?,
      dst: r.get::<_, Option<String>>(3)?,
    })
  })?;
  let mut out = Vec::new();
  for r in rows { out.push(r?); }
  Ok(out)
}
