use anyhow::{Result, anyhow, Context};
use tauri::{AppHandle, Manager};
use std::{path::PathBuf, fs, time::Duration};
use rand::RngCore;
use tauri_plugin_stronghold::stronghold::Stronghold as SH;
use blake3;
fn vault_path(app: &AppHandle) -> Result<PathBuf> {
  Ok(app.path().app_local_data_dir()?.join("xtr_api_vault.hold"))
}
fn salt_path(app: &AppHandle) -> Result<PathBuf> {
  Ok(app.path().app_local_data_dir()?.join("salt.txt"))
}
fn ensure_salt_file(app: &AppHandle) -> Result<Vec<u8>> {
  let p = salt_path(app)?;
  if !p.exists() {
    let mut salt = vec![0u8; 32];
    rand::rngs::OsRng.fill_bytes(&mut salt);
    fs::write(&p, &salt)?;
    Ok(salt)
  } else {
    Ok(fs::read(&p)?)
  }
}
fn derive_key(app: &AppHandle) -> Result<Vec<u8>> {
  let salt = ensure_salt_file(app)?;
  let mut ctx = Vec::new();
  ctx.extend_from_slice(b"xtr-gemini-default-password");
  ctx.extend_from_slice(&salt);
  let hash = blake3::hash(&ctx);
  Ok(hash.as_bytes().to_vec())
}
fn open_client(app: &AppHandle) -> Result<(SH, Vec<u8>, Vec<u8>)> {
  let key = derive_key(app)?;
  let snapshot = vault_path(app)?;
  let stronghold = SH::new(&snapshot, key.clone()).context("failed to init stronghold")?;
  let client_name = b"xtr_client".to_vec();
  // try load, otherwise create
  let _ = stronghold.inner().load_client(client_name.clone());
  let _ = stronghold.inner().create_client(client_name.clone());
  Ok((stronghold, client_name, key))
}
pub fn load_gemini_api_key(app: &AppHandle) -> Result<String> {
  let (stronghold, client_name, _key) = open_client(app)?;
  let client = stronghold.inner().get_client(client_name).map_err(|e| anyhow!(format!("{e:?}")))?;
  if let Some(bytes) = client.store().get(b"gemini/api_key").map_err(|e| anyhow!(format!("{e:?}")))? {
    let s = String::from_utf8(bytes).context("api key not valid utf8")?;
    Ok(s)
  } else {
    Err(anyhow!("API key not set; call set_api_key_once first"))
  }
}
pub fn store_gemini_api_key(app: &AppHandle, key_str: &str) -> Result<()> {
  if key_str.trim().is_empty() {
    return Err(anyhow!("empty api key"));
  }
  let (stronghold, client_name, _key) = open_client(app)?;
  let client = stronghold.inner().get_client(client_name).map_err(|e| anyhow!(format!("{e:?}")))?;
  client.store()
    .insert(b"gemini/api_key".to_vec(), key_str.as_bytes().to_vec(), None::<Duration>)
    .map_err(|e| anyhow!(format!("{e:?}")))?;
  stronghold.save().context("failed to save stronghold")?;
  Ok(())
}
