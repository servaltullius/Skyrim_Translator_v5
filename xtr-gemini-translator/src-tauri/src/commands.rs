use tauri::{AppHandle, Emitter};
use anyhow::anyhow;
use serde::{Serialize, Deserialize};
use crate::{pipeline, db};
#[derive(Serialize, Deserialize, Debug, Clone)]
pub struct TranslateOpts {
  pub concurrency: Option<usize>,
  #[serde(rename = "batchSize")]
  pub batch_size: Option<usize>,
  pub model: Option<String>,
  pub temperature: Option<f32>,
}
#[tauri::command]
pub async fn open_xml(app: AppHandle, path: String) -> tauri::Result<usize> {
  let count = crate::xml::load_xtranslator_xml(&app, &path).await?;
  Ok(count)
}
#[derive(Serialize)]
pub struct SimpleRow {
  pub id: i64,
  pub status: String,
  pub src: String,
  pub dst: Option<String>,
}
#[tauri::command]
pub fn get_rows(app: AppHandle, limit: Option<usize>, offset: Option<usize>) -> tauri::Result<Vec<SimpleRow>> {
  let lim = limit.unwrap_or(1000);
  let off = offset.unwrap_or(0);
  let items = db::list_entries(&app, lim, off)?;
  Ok(items.into_iter().map(|r| SimpleRow { id: r.id, status: r.status, src: r.src, dst: r.dst }).collect())
}
#[tauri::command]
pub async fn start_translate(app: AppHandle, opts: Option<TranslateOpts>) -> tauri::Result<()> {
  pipeline::start(app, opts).await?;
  Ok(())
}
#[tauri::command]
pub fn update_entry_dst(app: AppHandle, id: i64, dst: String) -> tauri::Result<()> {
  crate::db::update_entry_done(&app, id, &dst)?;
  Ok(())
}
#[tauri::command]
pub fn clear_entry_dst(app: AppHandle, id: i64) -> tauri::Result<()> {
  crate::db::clear_entry_dst(&app, id)?;
  Ok(())
}
#[tauri::command]
pub fn export_errors(_app: AppHandle, path: String, data: serde_json::Value) -> Result<bool, String> {
  let buf = serde_json::to_vec_pretty(&data).map_err(|e| e.to_string())?;
  std::fs::write(&path, buf).map_err(|e| e.to_string())?;
  Ok(true)
}
#[tauri::command]
pub async fn cancel_translate(app: AppHandle) -> tauri::Result<bool> {
  Ok(pipeline::cancel(&app).await)
}
#[tauri::command]
pub async fn save_xml(app: AppHandle, path: Option<String>) -> tauri::Result<usize> {
  let written = crate::xml::write_xtranslator_xml(&app, path.as_deref()).await?;
  Ok(written)
}
#[tauri::command]
pub fn get_glossary(app: AppHandle) -> tauri::Result<serde_json::Value> {
  Ok(db::get_glossary(&app)?)
}
#[tauri::command]
pub fn upsert_glossary(app: AppHandle, json: serde_json::Value) -> tauri::Result<()> {
  db::upsert_glossary(&app, json)?;
  Ok(())
}
#[tauri::command]
pub fn get_settings(app: AppHandle) -> tauri::Result<serde_json::Value> {
  Ok(db::get_settings(&app)?)
}
#[tauri::command]
pub fn update_settings(app: AppHandle, patch: serde_json::Value) -> tauri::Result<()> {
  db::update_settings(&app, patch)?;
  Ok(())
}
#[derive(Serialize, Deserialize)]
pub struct ApiKeyInput { #[serde(rename = "apiKey")] pub api_key: Option<String> }
#[tauri::command]
pub fn set_api_key_once(app: AppHandle, input: Option<ApiKeyInput>) -> tauri::Result<serde_json::Value> {
  if let Some(key) = input.and_then(|i| i.api_key) {
    crate::secure::store_gemini_api_key(&app, &key)?;
    Ok(serde_json::json!({ "stored": true }))
  } else {
    Ok(serde_json::json!({ "stored": false }))
  }
}
