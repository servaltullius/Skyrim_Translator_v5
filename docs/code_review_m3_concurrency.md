# Code Review: M3 Concurrency, Cancellation, Backoff, Stronghold+LLM Wiring
Date: 2025-08-26
Owner: Alduin Builder
Scope: Implement concurrency-limited, cancellable pipeline with retry/backoff; wire Stronghold secrets; integrate Gemini REST; minimal tests.
 
## Changes
- Pipeline
  - File: `src-tauri/src/pipeline.rs`
  - Adds bounded concurrency with `tokio::sync::Semaphore` at batch level.
  - Uses `FuturesUnordered` to run multiple batches concurrently.
  - Adds `CancellationToken` (tokio-util) with global holder; `cancel_translate` triggers cancel.
  - Retry/backoff: per-batch exponential backoff (base 500ms, cap 5s, max retries 3) on network errors.
  - Emits progress per row using `AtomicUsize` counter; preserves existing events (`row_translated`, `job_progress`, `job_error`, `job_done`).
- Stronghold secrets
  - File: `src-tauri/src/secure.rs`
  - Argon2id KDF with salt file (`salt.txt`) under app local data dir.
  - Snapshot `xtr_api_vault.hold`; client `xtr_client`; store key `gemini/api_key`.
  - Functions: `store_gemini_api_key`, `load_gemini_api_key`.
  - Cargo: enable `tauri-plugin-stronghold` feature `kdf`.
  - Lib: plugin `with_argon2` already initialized in `lib.rs`.
- LLM client
  - File: `src-tauri/src/llm/gemini.rs`
  - Implements `:generateContent` REST using `reqwest` and joins/splits with sentinel `<<<#SEP#>>>`.
- XML/DB helpers
  - Existing loader/writer from prior step; DB: `reset_entries`, `count_entries`, `list_entries`.
- IPC/API
  - Commands: `get_rows(limit?, offset?)` for initial UI hydration.
  - Front: `api.getRows`, `api.setApiKeyOnce`, temporary “API 키 설정” UI button.
- Utilities + Tests
  - File: `src-tauri/src/util.rs` with `exp_backoff_ms()` + unit tests ensuring doubling/capping behavior.
  - Existing textops unit tests intact.
- Cargo deps
  - Adds: `tokio-util` (sync), `once_cell`, `futures`, `argon2`, `rand`; updates `tokio` features to include `time`.
 
## Reasoning Summary
- Batch-level concurrency matches REST quotas and reduces HTTP overhead while exploiting parallelism.
- Cancellation token avoids spawning unbounded work post-cancel; tasks check token before/after requests.
- Backoff stabilizes under transient faults (429/5xx); cap prevents long sleeps.
- Stronghold stores API key backend-only; Argon2id+salt for snapshot key hardness; key never surfaced to frontend.
- Progress events use atomic counter to avoid race conditions across tasks.
 
## Risks and Mitigations
- AppHandle across tasks: Tauri v2 `AppHandle` is `Send + Sync`; operations are short; DB opens per call (rusqlite safe).
- Response split mismatch: If LLM returns fewer parts than inputs, code guards with default empty string; validator catches anomalies.
- Global cancellation state: simple `Mutex<Option<CancellationToken>>` avoids stale tokens; cancel clears state.
 
## How to Verify
1. Launch app; click “API 키 설정” and provide Gemini API key.
2. Open sample XML; confirm grid populates and `settings.xml_params` saved.
3. Click “번역 시작”; observe row-by-row updates and progress; trigger “취소” mid-run to stop quickly.
4. Disconnect/limit network to force retries; observe staggered `job_error` on max retries.
5. Save XML; validate that Params and attributes are preserved.
 
## Follow-ups
- Fine-grained retry by HTTP status (429/5xx) and jitter.
- Token bucket rate limiter for request pacing when needed.
- Full validator for ICU and XML stack; richer `ErrorCode`.
- UI polish: dedicated settings panel for API key (input masking), logs view, better toasts.
 
