use regex::Regex;
use once_cell::sync::Lazy;
use crate::textops::mask::Masked;
static RE_TAG: Lazy<Regex> = Lazy::new(|| Regex::new(r"<[^>]+>").unwrap());
static RE_TAG_NAME: Lazy<Regex> = Lazy::new(|| Regex::new(r"^</?\s*([A-Za-z0-9:_-]+)").unwrap());
static RE_SELF_CLOSE: Lazy<Regex> = Lazy::new(|| Regex::new(r"/\s*>$").unwrap());
static RE_VAR: Lazy<Regex> = Lazy::new(|| Regex::new(r"(\{[^}]+\}|%[sd]|%\d*\$s|\$\d+)").unwrap());
static RE_TOKEN_LEFT: Lazy<Regex> = Lazy::new(|| Regex::new(r"⟦[TV]\d+⟧").unwrap());
static RE_ICU_HEADER: Lazy<Regex> = Lazy::new(|| Regex::new(r"^\{([^,{}]+),\s*(plural|select|selectordinal)\s*,").unwrap());
/// Backwards-compatible simple check (tag stack only).
pub fn validate_xml_safety(s: &str) -> bool {
  validate_tag_stack(s)
}
#[derive(Debug, thiserror::Error)]
pub enum ValidationError {
  #[error("leftover_tokens")]
  LeftoverTokens,
  #[error("unbalanced_tags at {pos}")]
  UnbalancedTags { pos: usize },
  #[error("tag_count_mismatch expected={expected} found={found}")]
  TagCountMismatch { expected: usize, found: usize },
  #[error("placeholder_count_mismatch expected={expected} found={found}")]
  PlaceholderCountMismatch { expected: usize, found: usize },
  #[error("icu_brace_unbalanced at {pos}")]
  ICUBraceUnbalanced { pos: usize },
  #[error("icu_other_missing at {pos}")]
  ICUOtherMissing { pos: usize },
  #[error("icu_invalid_selector '{key}' for {kind} at {pos}")]
  ICUInvalidSelector { pos: usize, key: String, kind: String },
}
/// Full validation with original masked context:
/// - No leftover tokens (⟦Tn⟧/⟦Vn⟧)
/// - Tag balance and ordering OK
/// - Tag occurrence count equals original masked count
/// - Placeholder occurrences equal original masked var count
/// - ICU braces balanced; if ICU patterns present, ensure `other{` exists
pub fn validate_with_mask(masked: &Masked, restored: &str) -> Result<(), ValidationError> {
  if RE_TOKEN_LEFT.is_match(restored) {
    return Err(ValidationError::LeftoverTokens);
  }
  if !validate_tag_stack(restored) {
    return Err(ValidationError::UnbalancedTags { pos: 0 });
  }
  let cur_tag_count = RE_TAG.find_iter(restored).count();
  if cur_tag_count != masked.tags.len() {
    return Err(ValidationError::TagCountMismatch { expected: masked.tags.len(), found: cur_tag_count });
  }
  let cur_var_count = RE_VAR.find_iter(restored).count();
  if cur_var_count != masked.vars.len() {
    return Err(ValidationError::PlaceholderCountMismatch { expected: masked.vars.len(), found: cur_var_count });
  }
  validate_icu(restored)?;
  Ok(())
}
fn validate_tag_stack(s: &str) -> bool {
  let mut stack: Vec<String> = Vec::new();
  for m in RE_TAG.find_iter(s) {
    let tag = m.as_str();
    // ignore comments/processing if any
    if tag.starts_with("<!--") || tag.starts_with("<?") {
      continue;
    }
    if RE_SELF_CLOSE.is_match(tag) {
      continue;
    }
    if let Some(cap) = RE_TAG_NAME.captures(tag) {
      let name = cap.get(1).map(|x| x.as_str()).unwrap_or("").to_string();
      if tag.starts_with("</") {
        // closing
        if let Some(last) = stack.pop() {
          if last.to_ascii_lowercase() != name.to_ascii_lowercase() {
            return false;
          }
        } else {
          return false;
        }
      } else {
        // opening
        stack.push(name);
      }
    }
  }
  stack.is_empty()
}
fn validate_icu(s: &str) -> Result<(), ValidationError> {
  // Global brace balance check with pos hint
  let mut depth = 0i32;
  for (i, ch) in s.char_indices() {
    match ch {
      '{' => depth += 1,
      '}' => {
        depth -= 1;
        if depth < 0 {
          return Err(ValidationError::ICUBraceUnbalanced { pos: i });
        }
      }
      _ => {}
    }
  }
  if depth != 0 {
    // position unknown: report end
    return Err(ValidationError::ICUBraceUnbalanced { pos: s.len().saturating_sub(1) });
  }
  // Per-block checks
  for (start, end, kind, header_end) in find_icu_blocks(s) {
    let block = &s[start..end];
    let kind_s = kind.to_string();
    // Extract top-level selectors within this block content
    let content = &s[header_end..end - 1]; // exclude closing brace
    let keys = extract_icu_keys(content);
    // Require 'other' key for all ICU kinds
    if !keys.iter().any(|k| k == "other") {
      return Err(ValidationError::ICUOtherMissing { pos: start });
    }
    // Validate allowed keys for plural/selectordinal
    if kind == "plural" || kind == "selectordinal" {
      let allowed = ["zero", "one", "two", "few", "many", "other"];
      for k in keys {
        if !allowed.contains(&k.as_str()) {
          return Err(ValidationError::ICUInvalidSelector { pos: start, key: k, kind: kind_s.clone() });
        }
      }
    }
    // select: allow any labels (cannot validate here)
    let _ = block; // silence unused warning
  }
  Ok(())
}
fn find_icu_blocks(s: &str) -> Vec<(usize, usize, &'static str, usize)> {
  // returns (start, end_exclusive, kind, header_end_index)
  let mut out = Vec::new();
  let bytes = s.as_bytes();
  let mut i = 0usize;
  while i < bytes.len() {
    if bytes[i] == b'{' {
      let rest = &s[i..];
      if let Some(cap) = RE_ICU_HEADER.captures(rest) {
        let kind = cap.get(2).unwrap().as_str();
        let header_len = cap.get(0).unwrap().end(); // relative to rest
        // find matching closing brace
        let mut depth = 0i32;
        let mut j = i;
        while j < bytes.len() {
          let ch = bytes[j] as char;
          if ch == '{' {
            depth += 1;
          } else if ch == '}' {
            depth -= 1;
            if depth == 0 {
              // closing brace of this block at j
              let end_ex = j + 1;
              out.push((i, end_ex, if kind == "plural" { "plural" } else if kind == "select" { "select" } else { "selectordinal" }, i + header_len));
              i = end_ex;
              break;
            }
          }
          j += 1;
        }
        if j >= bytes.len() {
          // unbalanced; report at start
          out.push((i, bytes.len(), "plural", i + header_len)); // will be caught by brace check
          i = bytes.len();
        }
        continue;
      }
    }
    i += 1;
  }
  out
}
fn extract_icu_keys(content: &str) -> Vec<String> {
  // parse top-level keys of ICU block content
  let mut keys = Vec::new();
  let bytes = content.as_bytes();
  let mut i = 0usize;
  let mut depth = 0i32;
  while i < bytes.len() {
    let ch = bytes[i] as char;
    match ch {
      '{' => { depth += 1; i += 1; }
      '}' => { depth -= 1; i += 1; }
      _ => {
        if depth == 0 && ch.is_ascii_alphabetic() {
          // read identifier
          let start = i;
          i += 1;
          while i < bytes.len() {
            let c = bytes[i] as char;
            if c.is_ascii_alphabetic() { i += 1; } else { break; }
          }
          let ident = &content[start..i];
          // skip spaces
          while i < bytes.len() && (bytes[i] as char).is_ascii_whitespace() {
            i += 1;
          }
          if i < bytes.len() && bytes[i] as char == '{' {
            // key open
            keys.push(ident.to_string());
            // we don't consume the '{' here; loop will handle depth increment on next iteration
          }
        } else {
          i += 1;
        }
      }
    }
  }
  keys
}
#[cfg(test)]
mod tests {
  use super::*;
  #[test]
  fn tag_stack_ok() {
    assert!(validate_tag_stack("<b>hi<i>x</i></b>"));
    assert!(!validate_tag_stack("<b>hi<i>x</b></i>"));
    assert!(validate_tag_stack("<img src='x'/>text"));
  }
  #[test]
  fn with_mask_counts() {
    let masked = Masked {
      text: "A ⟦T0⟧bold⟦T1⟧ {name}".into(),
      tags: vec!["<b>".into(), "</b>".into()],
      vars: vec!["{name}".into()],
    };
    let restored = "A <b>bold</b> {name}";
    assert!(validate_with_mask(&masked, restored).is_ok());
    let tampered = "A <b>bold</b> {name} <i>"; // extra tag
    assert!(validate_with_mask(&masked, tampered).is_err());
  }
  #[test]
  fn icu_basic() {
    let s = "{itemCount, plural, one{1} other{#}}";
    assert!(validate_icu(s).is_ok());
    let bad = "{itemCount, plural, one{1}}";
    assert!(validate_icu(bad).is_err());
  }
  #[test]
  fn icu_invalid_selector_for_plural() {
    let s = "{n, plural, bogus{X} other{Y}}";
    let e = validate_icu(s).unwrap_err();
    match e {
      ValidationError::ICUInvalidSelector { key, .. } => assert_eq!(key, "bogus"),
      _ => panic!("unexpected error {:?}", e),
    }
  }
  #[test]
  fn icu_nested_ok() {
    let s = "{n, plural, one{{x, select, a{A} other{B}}} other{Z}}";
    assert!(validate_icu(s).is_ok());
  }
}
