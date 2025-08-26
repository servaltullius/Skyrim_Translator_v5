# Perf Harness (50k Mock LLM)
This document explains how to run the 50k-row performance harness without calling external networks.
 
## App (interactive)
- In the app, open Sidebar and enable “모의 LLM”.
- Set concurrency/batch/RPS in Settings and click “설정 저장”.
- Open an XML file or generate data via your own helper; then click “번역 시작”.
- The header shows live metrics when available: p50/p95/rps.
 
## Headless (tests)
- Navigate to `xtr-gemini-translator/src-tauri`.
- Run: `cargo test -- --ignored`
- Test `perf_50k_mock_llm`:
  - Creates a temp DB, generates 50k synthetic rows (tags/placeholders/ICU mix).
  - Runs the mock pipeline (echo translator) with concurrency/batch limits.
  - Reports totals and asserts that all rows were processed.
 
## Tuning
- `settings.concurrency`: number of concurrent batches.
- `settings.batch_size`: number of rows per batch.
- `settings.rps`: global pacing using a simple rate limiter.
 
## Notes
- Mock mode exercises text ops (mask/restore/validate) and DB I/O without network variability.
- For real runs, disable “모의 LLM” and configure an API key (Stronghold).
 
