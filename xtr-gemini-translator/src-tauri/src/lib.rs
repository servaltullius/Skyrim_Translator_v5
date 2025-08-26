#![allow(unused)]
pub mod commands;
pub mod events;
pub mod db;
pub mod xml;
pub mod pipeline;
pub mod llm;
pub mod textops;
pub mod secure;
pub mod util;
pub mod bench;
use tauri::{Manager, Emitter};
pub fn run() {
  tauri::Builder::default()
    .plugin(tauri_plugin_dialog::init())
    .plugin(tauri_plugin_store::Builder::default().build())
    .setup(|app| {
      // Stronghold init (argon2); if unavailable, app still runs, but secure module will error on use
      let salt_path = app.path().app_local_data_dir().unwrap().join("salt.txt");
      let _ = app
        .handle()
        .plugin(tauri_plugin_stronghold::Builder::with_argon2(&salt_path).build());
      // DB init
      db::init(&app.handle())?;
      Ok(())
    })
    .invoke_handler(tauri::generate_handler![
      commands::open_xml,
      commands::get_rows,
      commands::start_translate,
      commands::cancel_translate,
      commands::save_xml,
      commands::get_glossary,
      commands::upsert_glossary,
      commands::get_settings,
      commands::update_settings,
      commands::set_api_key_once,
      commands::update_entry_dst,
      commands::clear_entry_dst,
      commands::export_errors,
    ])
    .run(tauri::generate_context!())
    .expect("error while running tauri application");
}
