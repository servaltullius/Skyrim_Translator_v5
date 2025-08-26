use tauri::{AppHandle, Emitter};
use anyhow::{Result, Context};
use crate::{db, events::*, textops};
use crate::llm::gemini::LlmCallError;
use std::sync::{Arc, atomic::{AtomicUsize, Ordering}, Mutex};
use once_cell::sync::Lazy;
use tokio::sync::Semaphore;
use tokio::time::{sleep, Duration};
use tokio_util::sync::CancellationToken;
use futures::StreamExt;
use tokio::time::Instant;
static CANCEL_TOK: Lazy<Mutex<Option<CancellationToken>>> = Lazy::new(|| Mutex::new(None));
pub async fn start(app: AppHandle, opts: Option<crate::commands::TranslateOpts>) -> Result<()> {
  // initialize cancellation token
  {
    let mut guard = CANCEL_TOK.lock().unwrap();
    *guard = Some(CancellationToken::new());
  }
  let t_start = Instant::now();
  // Load persisted settings and merge with provided opts
  let settings = db::get_settings(&app)?;
  let batch_size = opts.as_ref().and_then(|o| o.batch_size)
    .or_else(|| settings.get("batch_size").and_then(|v| v.as_u64()).map(|v| v as usize))
    .unwrap_or(8);
  let concurrency = opts.as_ref().and_then(|o| o.concurrency)
    .or_else(|| settings.get("concurrency").and_then(|v| v.as_u64()).map(|v| v as usize))
    .unwrap_or(10);
  let model = opts.as_ref().and_then(|o| o.model.clone())
    .or_else(|| settings.get("model").and_then(|v| v.as_str()).map(|s| s.to_string()))
    .unwrap_or_else(|| "gemini-1.5-flash".to_string());
  let temperature = opts.as_ref().and_then(|o| o.temperature)
    .or_else(|| settings.get("temperature").and_then(|v| v.as_f64()).map(|v| v as f32))
    .unwrap_or(0.2);
  let mock_llm = settings.get("mock_llm").and_then(|v| v.as_bool()).unwrap_or(false);
  let rows = db::select_pending(&app, batch_size.saturating_mul(512))?; // window some pages
  let total = rows.len();
  let done = Arc::new(AtomicUsize::new(0));
  let sem = Arc::new(Semaphore::new(concurrency));
  let latencies = Arc::new(tokio::sync::Mutex::new(Vec::<u128>::new()));
  // init LLM client (shared) and rate limiter
  let client = reqwest::Client::new();
  let gemini = if !mock_llm {
    let api_key = crate::secure::load_gemini_api_key(&app)?;
    Some(Arc::new(crate::llm::gemini::Gemini::new(client, api_key, model, temperature)))
  } else {
    None
  };
  let sys = Arc::new(crate::llm::prompt::build_system_prompt(&app)?);
  let settings = db::get_settings(&app)?;
  let rps = settings.get("rps").and_then(|v| v.as_u64()).unwrap_or(5) as u32;
  let rate_mode = settings.get("rate_mode").and_then(|v| v.as_str()).unwrap_or("gap").to_string();
  let limiter_gap = crate::util::RateLimiter::new_per_sec(rps.max(1));
  let bucket_cap = settings.get("bucket_capacity").and_then(|v| v.as_u64()).unwrap_or((rps * 2).max(1) as u64) as f64;
  let limiter_bucket = crate::util::TokenBucket::new(rps.max(1) as f64, bucket_cap);
  let redact_logs = settings.get("redact_logs").and_then(|v| v.as_bool()).unwrap_or(false);
  let app_handle = app.clone();
  let cancel = {
    let guard = CANCEL_TOK.lock().unwrap();
    guard.as_ref().unwrap().clone()
  };
  let mut futs = futures::stream::FuturesUnordered::new();
  for chunk in rows.chunks(batch_size) {
    let chunk_data: Vec<(i64, String)> = chunk.iter().map(|r| (r.id, r.src.clone())).collect();
    let permit = sem.clone().acquire_owned().await.unwrap();
    if cancel.is_cancelled() {
      break;
    }
    let gemini = gemini.clone();
    let sys = sys.clone();
    let app = app_handle.clone();
    let done_clone = done.clone();
    let cancel_clone = cancel.clone();
    // capture limiters and rate mode
    let rate_mode = rate_mode.clone();
    let limiter_gap = limiter_gap.clone();
    let limiter_bucket = limiter_bucket.clone();
    let latencies_c = latencies.clone();
    futs.push(tokio::spawn(async move {
      let _permit = permit; // keep until task ends
      if cancel_clone.is_cancelled() {
        return Ok::<(), anyhow::Error>(());
      }
      // mask
      let masked: Vec<_> = chunk_data.iter().map(|(id, src)| (*id, textops::mask::mask(src))).collect();
      let inputs: Vec<String> = masked.iter().map(|(_, m)| m.text.clone()).collect();
      // retry policy
      let max_retries = 3usize;
      let mut attempt = 0usize;
      let t_batch = Instant::now();
      let outputs: Vec<String> = loop {
        if cancel_clone.is_cancelled() {
          return Ok(());
        }
        // rate limit pacing
        match rate_mode.as_str() {
          "bucket" => { limiter_bucket.wait_one().await; }
          _ => { limiter_gap.wait().await; }
        }
        if let Some(g) = &gemini {
          match g.translate_batch(&inputs, &sys).await {
            Ok(v) => break v,
            Err(e) => {
              attempt += 1;
              if attempt >= max_retries {
                // mark all rows as error
                for (i, (id, _)) in masked.iter().enumerate() {
                  let _ = db::update_entry_error(&app, *id, "network_error");
                  let _ = app.emit(EVT_JOB_ERROR, serde_json::json!({ "id": id, "code": "network_error", "message": e.to_string() }));
                  let cur = done_clone.fetch_add(1, Ordering::Relaxed) + 1;
                  let _ = app.emit(EVT_JOB_PROGRESS, serde_json::json!({ "done": cur, "total": total }));
                }
                return Ok(());
              }
              // classify error for smarter backoff
              let (base_ms, cap_ms, retry_after) = match &e {
                LlmCallError::RateLimited { retry_after } => {
                  let ra = retry_after.map(|d| d.as_millis() as u64);
                  (1000, 10_000, ra)
                }
                LlmCallError::Server(_) => (700, 8_000, None),
                LlmCallError::Client(_) => (500, 5_000, None),
                LlmCallError::Network(_) => (500, 5_000, None),
                LlmCallError::Decode(_) => (500, 5_000, None),
              };
              let delay_ms = if let Some(ra) = retry_after {
                ra
              } else {
                crate::util::exp_backoff_ms(attempt, base_ms, cap_ms)
              };
              let delay_ms = crate::util::add_jitter_ms(delay_ms, 250);
              sleep(Duration::from_millis(delay_ms)).await;
            }
          }
        } else {
          // mock mode: echo inputs
          break inputs.clone();
        }
      };
      for (i, (id, m)) in masked.iter().enumerate() {
        if cancel_clone.is_cancelled() {
          return Ok(());
        }
        let t0 = Instant::now();
        let restored = textops::restore::restore(m, &outputs.get(i).cloned().unwrap_or_default());
        let vres = textops::validate::validate_with_mask(m, &restored);
        if vres.is_ok() {
          let _ = db::update_entry_done(&app, *id, &restored);
          let _ = app.emit(EVT_ROW_TRANSLATED, serde_json::json!({ "id": id, "dst": restored, "status": "done" }));
        } else {
          // classify validation error
          let err = vres.err().unwrap();
          let (code, msg) = match &err {
            crate::textops::validate::ValidationError::LeftoverTokens => ("leftover_tokens", err.to_string()),
          crate::textops::validate::ValidationError::UnbalancedTags { .. } => ("unbalanced_tags", err.to_string()),
          crate::textops::validate::ValidationError::TagCountMismatch { .. } => ("tag_count_mismatch", err.to_string()),
          crate::textops::validate::ValidationError::PlaceholderCountMismatch { .. } => ("placeholder_count_mismatch", err.to_string()),
          crate::textops::validate::ValidationError::ICUBraceUnbalanced { .. } => ("icu_brace_unbalanced", err.to_string()),
          crate::textops::validate::ValidationError::ICUOtherMissing { .. } => ("icu_other_missing", err.to_string()),
          crate::textops::validate::ValidationError::ICUInvalidSelector { .. } => ("icu_invalid_selector", err.to_string()),
          };
          let _ = db::update_entry_error_ex(&app, *id, code, Some(&msg));
          let preview = if redact_logs { String::new() } else { if restored.len() > 2000 { format!("{}…", &restored[..2000]) } else { restored.clone() } };
          // try to extract pos from message e.g., "... at 123"
          let pos = msg.rsplit(' ').next().and_then(|w| w.parse::<usize>().ok());
          let _ = app.emit(EVT_JOB_ERROR, serde_json::json!({ "id": id, "code": code, "message": msg, "pos": pos, "preview": preview }));
        }
        let cur = done_clone.fetch_add(1, Ordering::Relaxed) + 1;
        let _ = app.emit(EVT_JOB_PROGRESS, serde_json::json!({ "done": cur, "total": total }));
        let dt = t0.elapsed().as_millis();
        {
          let mut v = latencies_c.lock().await;
          v.push(dt);
        }
      }
      Ok(())
    }));
  }
  // await all tasks
  while let Some(res) = futs.next().await {
    let _ = res; // ignore task-level errors already handled
  }
  let done_final = done.load(Ordering::Relaxed);
  // compute metrics
  let mut v = latencies.lock().await;
  v.sort_unstable();
  let p50 = if v.is_empty() { 0 } else { v[(v.len() as f64 * 0.50) as usize] };
  let p95 = if v.is_empty() { 0 } else { v[((v.len() as f64 * 0.95) as usize).min(v.len().saturating_sub(1))] };
  let duration_ms = t_start.elapsed().as_millis();
  let rps = if duration_ms == 0 { 0.0 } else { (done_final as f64) / (duration_ms as f64 / 1000.0) };
  app_handle.emit(EVT_JOB_METRICS, serde_json::json!({ "p50_ms": p50, "p95_ms": p95, "duration_ms": duration_ms, "rps": rps }))?;
  app_handle.emit(EVT_JOB_DONE, serde_json::json!({ "done": done_final, "total": total, "durationMs": duration_ms }))?;
  Ok(())
}
pub async fn cancel(_app: &AppHandle) -> bool {
  let tok = {
    let mut guard = CANCEL_TOK.lock().unwrap();
    guard.take()
  };
  if let Some(t) = tok {
    t.cancel();
    true
  } else {
    false
  }
}
