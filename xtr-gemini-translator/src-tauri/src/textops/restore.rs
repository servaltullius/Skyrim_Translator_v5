use super::mask::Masked;
use regex::Regex;
use similar::TextDiff;
/// Extract tokens like ⟦T0⟧, ⟦V0⟧ from text in their order of appearance.
fn extract_tokens(text: &str) -> Vec<String> {
  static RE_TOK: once_cell::sync::Lazy<Regex> = once_cell::sync::Lazy::new(|| Regex::new(r"⟦[TV]\d+⟧").unwrap());
  RE_TOK.find_iter(text).map(|m| m.as_str().to_string()).collect()
}
enum Seg {
  Text(String),
  Token(String),
}
fn split_segments(text: &str) -> Vec<Seg> {
  static RE_TOK: once_cell::sync::Lazy<Regex> = once_cell::sync::Lazy::new(|| Regex::new(r"⟦[TV]\d+⟧").unwrap());
  let mut segs = Vec::new();
  let mut last = 0usize;
  for m in RE_TOK.find_iter(text) {
    if m.start() > last {
      segs.push(Seg::Text(text[last..m.start()].to_string()));
    }
    segs.push(Seg::Token(m.as_str().to_string()));
    last = m.end();
  }
  if last < text.len() {
    segs.push(Seg::Text(text[last..].to_string()));
  }
  if segs.is_empty() {
    segs.push(Seg::Text(text.to_string()));
  }
  segs
}
/// Normalize tokens in translated masked text to match the source token set and order:
/// - Drop tokens not present in source
/// - Drop duplicate tokens (keep first occurrence)
/// - Insert missing tokens at positions anchored by nearest present neighbor based on source order
fn normalize_tokens(src_masked: &str, translated_masked: &str) -> String {
  let source_tokens = extract_tokens(src_masked);
  let source_set: std::collections::HashSet<_> = source_tokens.iter().cloned().collect();
  let mut used: std::collections::HashSet<String> = std::collections::HashSet::new();
  let mut out: Vec<Seg> = Vec::new();
  // 1) Keep only allowed tokens and drop duplicates
  for seg in split_segments(translated_masked) {
    match seg {
      Seg::Token(tok) => {
        if source_set.contains(&tok) && !used.contains(&tok) {
          out.push(Seg::Token(tok.clone()));
          used.insert(tok);
        } else {
          // drop unknown/duplicate
        }
      }
      Seg::Text(t) => out.push(Seg::Text(t)),
    }
  }
  // 2) Insert missing tokens using neighbor anchors from source order
  // Build index of present tokens in out
  let mut present_positions: std::collections::HashMap<String, usize> = std::collections::HashMap::new();
  for (i, seg) in out.iter().enumerate() {
    if let Seg::Token(tok) = seg {
      present_positions.insert(tok.clone(), i);
    }
  }
  for tok in source_tokens.iter() {
    if used.contains(tok) {
      continue;
    }
    // Find previous present token in source order
    let idx_in_source = source_tokens.iter().position(|t| t == tok).unwrap_or(0);
    // search backward
    let mut inserted = false;
    if idx_in_source > 0 {
      for j in (0..idx_in_source).rev() {
        let prev = &source_tokens[j];
        if let Some(&pos) = present_positions.get(prev) {
          // insert after prev
          out.insert(pos + 1, Seg::Token(tok.clone()));
          // re-index present_positions after insertion
          let mut new_positions = present_positions.clone();
          for (_k, v) in new_positions.iter_mut() {
            if *v > pos { *v += 1; }
          }
          new_positions.insert(tok.clone(), pos + 1);
          present_positions = new_positions;
          used.insert(tok.clone());
          inserted = true;
          break;
        }
      }
    }
    if !inserted {
      // search forward
      for j in idx_in_source+1..source_tokens.len() {
        let next = &source_tokens[j];
        if let Some(&pos) = present_positions.get(next) {
          // insert before next (i.e., at its position)
          out.insert(pos, Seg::Token(tok.clone()));
          // re-index
          let mut new_positions = present_positions.clone();
          for (_k, v) in new_positions.iter_mut() {
            if *v >= pos { *v += 1; }
          }
          new_positions.insert(tok.clone(), pos);
          present_positions = new_positions;
          used.insert(tok.clone());
          inserted = true;
          break;
        }
      }
    }
    if !inserted {
      // no anchors exist; append at end (best-effort)
      out.push(Seg::Token(tok.clone()));
      let pos = out.len() - 1;
      present_positions.insert(tok.clone(), pos);
      used.insert(tok.clone());
    }
  }
  // 3) Join
  let mut s = String::new();
  for seg in out {
    match seg {
      Seg::Text(t) => s.push_str(&t),
      Seg::Token(t) => s.push_str(&t),
    }
  }
  s
}
pub fn restore(masked: &Masked, translated: &str) -> String {
  // First normalize tokens on the masked plane
  let normalized = normalize_tokens(&masked.text, translated);
  // Then replace markers with real tags/vars
  let mut out = normalized;
  for (i, t) in masked.tags.iter().enumerate() {
    let token = format!("⟦T{}⟧", i);
    out = out.replace(&token, t);
  }
  for (i, v) in masked.vars.iter().enumerate() {
    let token = format!("⟦V{}⟧", i);
    out = out.replace(&token, v);
  }
  out
}
#[allow(unused)]
pub fn refine_with_diff(src_text: &str, dst_text: &str) -> String {
  // Currently unused in normalize algorithm; placeholder for future diff-based insertion anchors
  let _diff = TextDiff::configure()
    .algorithm(similar::Algorithm::Patience)
    .diff_chars(src_text, dst_text);
  dst_text.to_string()
}
#[cfg(test)]
mod tests {
  use super::*;
  use crate::textops::mask;
  #[test]
  fn drops_duplicate_tokens() {
    let src = "A ⟦T0⟧bold⟦T1⟧";
    let out = normalize_tokens(src, "A ⟦T0⟧ ⟦T0⟧ bold ⟦T1⟧");
    assert_eq!(extract_tokens(&out), vec!["⟦T0⟧".to_string(), "⟦T1⟧".to_string()]);
  }
  #[test]
  fn inserts_missing_after_anchor() {
    // Source has token order T0 then V0; output missed V0
    let m = mask::Masked {
      text: "A ⟦T0⟧ bold ⟦V0⟧ text".into(),
      tags: vec!["<b>".into()],
      vars: vec!["{name}".into()],
    };
    let restored = restore(&m, "A ⟦T0⟧ bold text");
    // After restore, {name} should appear somewhere after <b>
    assert!(restored.contains("<b>"));
    assert!(restored.contains("{name}"));
    let pos_b = restored.find("<b>").unwrap_or(0);
    let pos_var = restored.find("{name}").unwrap_or(usize::MAX);
    assert!(pos_var >= pos_b, "placeholder should be after anchor tag");
  }
}
