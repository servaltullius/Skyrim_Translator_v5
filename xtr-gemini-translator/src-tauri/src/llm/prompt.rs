use tauri::AppHandle;
pub fn build_system_prompt(app: &AppHandle) -> anyhow::Result<String> {
  let tpl = include_str!("../../../prompts/base.vec.txt");
  let glossary: serde_json::Value = crate::db::get_glossary(app)?;
  let pairs: Vec<String> = glossary.as_array().unwrap_or(&vec![])
    .iter().filter_map(|it| {
      Some(format!("{} => {}", it.get("key")?.as_str()?, it.get("value")?.as_str()?))
    }).collect();
  let base = tpl.replace("{{GLOSSARY}}", &pairs.join("\n"));
  let settings = crate::db::get_settings(app)?;
  let custom = settings.get("custom_prompt").and_then(|v| v.as_str()).unwrap_or("");
  let merged = if custom.is_empty() {
    base
  } else {
    format!("{base}\n\n[USER]\n{custom}")
  };
  Ok(merged)
}
