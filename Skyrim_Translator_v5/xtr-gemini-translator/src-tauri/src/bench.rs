use anyhow::Result;
use rusqlite::{Connection, params};
use std::path::Path;
use std::sync::{Arc, atomic::{AtomicUsize, Ordering}};
use tokio::sync::Semaphore;
use tokio::time::{Instant, Duration, sleep};
use futures::StreamExt;
use crate::textops::{mask, restore, validate};
#[derive(Debug, Clone)]
pub struct BenchSettings {
  pub concurrency: usize,
  pub batch_size: usize,
}
#[derive(Debug, Clone)]
pub struct PerfReport {
  pub total: usize,
  pub done: usize,
  pub duration_ms: u128,
  pub rps: f64,
  pub p50_ms: u128,
  pub p95_ms: u128,
}
pub fn generate_synthetic(conn: &Connection, n: usize) -> Result<()> {
  let tx = conn.unchecked_transaction()?;
  for i in 0..n {
    let src = if i % 3 == 0 {
      format!("Hello, <b>{{name}}</b> %d $1 — line {}", i)
    } else if i % 5 == 0 {
      "{n, plural, one{1 item} other{# items}}".to_string()
    } else {
      format!("Plain text line {}", i)
    };
    tx.execute(
      "INSERT INTO entries(id, src, status) VALUES(NULL, ?1, 'pending')",
      params![src],
    )?;
  }
  tx.commit()?;
  Ok(())
}
pub async fn run_bench(db_path: &Path, limit: usize, settings: BenchSettings) -> Result<PerfReport> {
  let conn = Connection::open(db_path)?;
  // count pending
  let mut st = conn.prepare("SELECT id, src FROM entries WHERE status='pending' LIMIT ?1")?;
  let rows = st.query_map(params![limit as i64], |r| {
    Ok((r.get::<_, i64>(0)?, r.get::<_, String>(1)?))
  })?;
  let mut inputs = Vec::new();
  for r in rows { inputs.push(r?); }
  let total = inputs.len();
  let sem = Arc::new(Semaphore::new(settings.concurrency));
  let done = Arc::new(AtomicUsize::new(0));
  let start = Instant::now();
  let mut futs = futures::stream::FuturesUnordered::new();
  let mut latencies: Vec<u128> = Vec::with_capacity(total);
  let lat_ptr = Arc::new(tokio::sync::Mutex::new(Vec::with_capacity(total)));
  for chunk in inputs.chunks(settings.batch_size) {
    let chunk_vec = chunk.to_vec();
    let permit = sem.clone().acquire_owned().await.unwrap();
    let conn_path = db_path.to_path_buf();
    let done_c = done.clone();
    let lat_ptr_c = lat_ptr.clone();
    futs.push(tokio::spawn(async move {
      let _permit = permit;
      let conn = Connection::open(conn_path).unwrap();
      let masked: Vec<_> = chunk_vec.iter().map(|(_id, src)| mask::mask(src)).collect();
      // Mock LLM: echo masked
      let outputs: Vec<String> = masked.iter().map(|m| m.text.clone()).collect();
      for (i, (id, _src)) in chunk_vec.iter().enumerate() {
        let t0 = Instant::now();
        let restored = restore::restore(&masked[i], &outputs[i]);
        let _ = crate::db::update_entry_done_with_conn(&conn, *id, &restored);
        let _ = validate::validate_xml_safety(&restored);
        let dt = t0.elapsed().as_millis();
        {
          let mut v = lat_ptr_c.lock().await;
          v.push(dt);
        }
        done_c.fetch_add(1, Ordering::Relaxed);
      }
      Ok::<(), anyhow::Error>(())
    }));
  }
  while let Some(_r) = futs.next().await { }
  let duration_ms = start.elapsed().as_millis();
  let done_n = done.load(Ordering::Relaxed);
  // compute percentiles
  {
    let mut v = lat_ptr.lock().await;
    v.sort_unstable();
    let p50 = if v.is_empty() { 0 } else { v[(v.len() as f64 * 0.50) as usize] };
    let p95 = if v.is_empty() { 0 } else { v[((v.len() as f64 * 0.95) as usize).min(v.len()-1)] };
    let rps = if duration_ms == 0 { 0.0 } else { (done_n as f64) / (duration_ms as f64 / 1000.0) };
    return Ok(PerfReport {
      total,
      done: done_n,
      duration_ms,
      rps,
      p50_ms: p50 as u128,
      p95_ms: p95 as u128,
    })
  }
}
