use std::path::PathBuf;
use rusqlite::Connection;
use tempfile::tempdir;
use xtr_gemini::{db, bench};
#[tokio::test(flavor = "multi_thread", worker_threads = 4)]
#[ignore] // heavy test; run with `cargo test -- --ignored`
async fn perf_50k_mock_llm() {
  // Prepare temp db
  let dir = tempdir().unwrap();
  let db_path = dir.path().join("xtr.db");
  let conn = Connection::open(&db_path).unwrap();
  db::init_on_connection(&conn).unwrap();
  // Generate 50k synthetic rows
  bench::generate_synthetic(&conn, 50_000).unwrap();
  // Run headless bench with mock translator
  let report = bench::run_bench(&db_path, 50_000, bench::BenchSettings { concurrency: 12, batch_size: 16 }).await.unwrap();
  eprintln!("perf_50k report: {:?}", report);
  assert_eq!(report.done, 50_000);
  assert_eq!(report.total, 50_000);
}
