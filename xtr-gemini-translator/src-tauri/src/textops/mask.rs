use regex::Regex;
use serde::{Serialize, Deserialize};
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Masked {
  pub text: String,
  pub tags: Vec<String>,
  pub vars: Vec<String>,
}
pub fn mask(input: &str) -> Masked {
  let re_tag = Regex::new(r"<[^>]+>").unwrap();
  let re_var = Regex::new(r"(\{[^}]+\}|%[sd]|%\d*\$s|\$\d+)").unwrap();
  let mut cur = input.to_string();
  let mut tags = Vec::new();
  for (i, m) in re_tag.find_iter(input).enumerate() {
    tags.push(m.as_str().to_string());
    let token = format!("⟦T{}⟧", i);
    cur = cur.replacen(m.as_str(), &token, 1);
  }
  let mut count = 0usize;
  let mut vars = Vec::new();
  let out = re_var.replace_all(&cur, |caps: &regex::Captures| {
    let tok = format!("⟦V{}⟧", count);
    vars.push(caps.get(0).map(|m| m.as_str()).unwrap_or("").to_string());
    count += 1;
    tok
  }).to_string();
  Masked { text: out, tags, vars }
}
#[cfg(test)]
mod tests {
  use super::*;
  use crate::textops::restore;
  #[test]
  fn mask_and_restore_simple() {
    let s = "A <b>bold</b> move";
    let m = mask(s);
    assert!(m.text.contains("⟦T0⟧"));
    let tr = m.text.replace("A", "한");
    let out = restore::restore(&m, &tr);
    assert!(out.contains("<b>"));
    assert!(out.contains("</b>"));
  }
  #[test]
  fn placeholders_preserved() {
    let s = "Hello, {name}! Score: %d";
    let m = mask(s);
    let tr = m.text.replace("Hello", "안녕");
    let out = restore::restore(&m, &tr);
    assert!(out.contains("{name}"));
    assert!(out.contains("%d"));
  }
}
