# Design Review (M2): X-TR Gemini Translator (Alduin)
Owner: Alduin Builder
Source of Truth: AGENTS.md (governance), PRD.md (features), stub.md (scaffold)
Goal: Finalize architecture and specs before scaffold/implementation
Determinism: Default temperature 0.2; batching stable; event payloads fixed
Security: Stronghold for secrets; API keys never persisted or returned to frontend
Review Gate: Await explicit “continue” before Scaffold (M3)
 
## 1) Architecture Overview
- Backend (Rust, Tauri v2):
  - Modules: commands, events, db (rusqlite), xml (quick-xml/roxmltree), textops (mask/restore/validate), llm (Gemini via reqwest), pipeline (workers, batching, retries), secure (Stronghold access).
  - IPC: `#[tauri::command]` for requests; `emit` for real-time progress/events.
  - Storage: SQLite (rusqlite, bundled), WAL mode, indices. Stronghold for secrets.
  - Concurrency: Tokio runtime + Semaphore; retry with backoff; cancellation token.
- Frontend (React + TS, Vite):
  - State: Zustand store (rows slice, progress slice, filters).
  - UI: TanStack Table + react-window virtualization (50k+ rows), Sidebar (file, translate controls, glossary, prompt, settings), Progress/Log panel.
  - IPC: `@tauri-apps/api/core.invoke`, `@tauri-apps/api/event.listen`.
- Packaging (Windows 11):
  - Tauri v2 MSI target; WebView2 runtime presence check; include prompts/glossary in bundle resources.
 
## 2) IPC Spec (Commands & Events)
- Commands (naming is kebab-case in Tauri invoke; Rust uses snake_case):
  - `open_xml(path: string) -> { count: number, meta?: { sourcePath, timeMs } }`
  - `start_translate(opts?: TranslateOpts) -> { startedAt: string, jobId: string }`
  - `cancel_translate() -> { canceled: boolean }`
  - `save_xml(path?: string) -> { written: number, targetPath: string }`
  - `get_glossary() -> GlossaryJson`
  - `upsert_glossary(json: GlossaryJson) -> { ok: true }`
  - `get_settings() -> SettingsJson`
  - `update_settings(patch: SettingsJson) -> { ok: true }`
  - `set_api_key_once(input?: { apiKey?: string }) -> { stored: boolean }`
    - Contract: If provided, store immediately in Stronghold. Never return key. If not provided, return `{stored:false}`.
- Events (payloads are stable, documented):
  - `row_translated`: `{ id: number, dst: string, status: "done", warnings?: string[] }`
  - `job_progress`: `{ jobId: string, done: number, total: number, rate?: number }`
  - `job_error`: `{ jobId: string, id?: number, code: ErrorCode, message?: string }`
  - `job_done`: `{ jobId: string, done: number, total: number, durationMs: number }`
- ErrorCode enum:
  - `parse_failed`, `network_error`, `rate_limited`, `timeout`, `validate_failed`, `restore_mismatch`, `cancelled`, `unknown`
 
## 3) Data Model (SQLite rusqlite)
- Tables
  - `entries`:
    - `id INTEGER PRIMARY KEY`
    - `edid TEXT` (optional key)
    - `rec TEXT` (record/category)
    - `src TEXT NOT NULL`
    - `dst TEXT` (nullable)
    - `status TEXT NOT NULL DEFAULT 'pending'` // pending|done|error
    - `meta_json TEXT` // JSON: {len, hash, warnings[], errors[], retries, jobId}
    - `updated_at TEXT DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now'))`
  - `glossary(key TEXT PRIMARY KEY, value TEXT NOT NULL, note TEXT)`
  - `settings(key TEXT PRIMARY KEY, value TEXT NOT NULL)` // JSON-encoded values
- Indices
  - `CREATE INDEX idx_entries_status ON entries(status);`
  - (Optional) `CREATE INDEX idx_entries_edid ON entries(edid);`
- Settings keys (examples)
  - `model` (string, default "gemini-1.5-flash")
  - `temperature` (float, default 0.2)
  - `concurrency` (int, default 10)
  - `batch_size` (int, default 8)
  - `retry_policy` ({max, baseMs})
 
## 4) XML I/O Contract (xTranslator shape)
- Loader (FR1.1)
  - Use `quick-xml::Reader` in streaming mode.
  - Extract for each `<String>`: `src` (original), `dst` (existing translation if present), `edid`/`rec` (if available), additional attributes to `meta_json`.
  - Bulk insert with transaction and prepared statements; WAL mode enabled.
  - Return `{count}` and store `sourcePath` in `settings`.
- Saver (FR1.2)
  - Read rows ordered by `id`; rebuild `<String>` nodes preserving original non-text attributes from `meta_json`.
  - Use writer to produce well-formed XML; encoding preserved (UTF-8 assumed; non-UTF encodings: normalize to UTF-8).
  - Validate minimal structure before writing; on failure emit `job_error(parse_failed)`.
 
## 5) TextOps (Mask / Restore / Validate)
- Mask (FR2.1)
  - Targets:
    - XML tags: `<[^>]+>` (including attributes and nested self-closing)
    - Placeholders: `{[^}]+}`, `%[sd]`, `%\\d*\\$s`, `$\\d+`
    - ICU MessageFormat blocks: `{identifier, (plural|select|selectordinal),[^}]+}` (best-effort; nested braces handled by counter)
  - Tokens:
    - Tags → `⟦T{idx}⟧`, Vars → `⟦V{idx}⟧`, ICU blocks → `⟦M{idx}⟧`
  - Output: `Masked { text, tags[], vars[], icu[] }`
- Restore (FR2.3)
  - Strategy:
    - First-pass: direct token replacement by index.
    - Second-pass (optional refine): `similar::TextDiff` with Patience algorithm to stabilize around boundaries if indices drift.
  - Mismatch handling:
    - If token count differs, emit `restore_mismatch`; attempt conservative restore; mark entry `error` with details in `meta_json`.
- Validate (FR2.4)
  - Checks:
    - XML tags: balanced/ordered via stack; allowed self-closing; attributes not altered.
    - Placeholder count equality: before vs after (`vars` and ICU).
    - ICU: brace pairing, selector keywords present (`one|other|few|many|zero` as applicable), `#` literal not mangled.
  - Result: `Ok | Err(code, message)`; on Err, mark entry error and emit `job_error(validate_failed)`.
 
## 6) Pipeline (Concurrency, Batching, Retry, Cancel)
- Orchestration
  - Inputs: `TranslateOpts { concurrency?, batch_size?, model?, temperature? }`
  - Defaults: `{ concurrency:10, batch_size:8, model:"gemini-1.5-flash", temperature:0.2 }`
  - Load `pending` rows in pages; for each page:
    - Mask → Build prompt (base VEC + glossary subset + user prompt) → Batch request → Restore → Validate → Save.
  - Events:
    - Emit `row_translated` per row; `job_progress` per processed row; `job_done` at completion.
- Batching
  - Concatenate inputs joined by sentinel `<<<#SEP#>>>`; on response, split back.
  - Balance by character count to keep under model limits; split long items by UAX #29 sentence boundaries and rejoin.
- Retry/Backoff
  - Exponential backoff with jitter on `network_error`, `rate_limited`, `timeout`; max retries per item (`settings.retry_policy`).
  - Failed items marked `error` with `retries` count.
- Cancellation
  - `CancellationToken` stored in pipeline state; `cancel_translate` flips token; in-flight batches complete; queue drains; emits `job_error(cancelled)` for remaining unstarted items, then `job_done`.
 
## 7) Gemini Client (Reqwest, Stronghold)
- HTTP
  - `reqwest::Client` reused; TLS: rustls; JSON body as per `:generateContent`.
  - Timeouts and retryable status codes (429, 5xx) handled by pipeline policy.
- Secrets
  - API key path: Stronghold vault key `"gemini/api_key"`.
  - Backend-only accessors:
    - `secure::load_gemini_api_key(&AppHandle) -> Result<String>`
    - `secure::store_gemini_api_key(&AppHandle, key: &str) -> Result<()>`
  - Frontend must never receive or persist the key. If a UI input is used, it is sent once via `set_api_key_once` and immediately zeroized in JS after invoke (implementation note).
 
## 8) Frontend Design
- Data Grid
  - TanStack Table v8 + react-window; row height fixed (e.g., 36px), `overscanCount: 8–12`.
  - Columns: `id`, `status`, `src`, `dst` (inline edit).
  - Batched state updates: buffer event payloads and apply at animation frames to avoid layout thrash.
- Sidebar
  - File open/save (dialog plugin), start/cancel controls, filters (pending/done/error), glossary editor (JSON table), prompt editor (VEC base + user).
- Progress/Log
  - Progress bar and stats (done/total/rate).
  - Error list with quick filter to problematic rows.
- A11y/UX
  - Keyboard shortcuts (Ctrl+S save, Ctrl+Enter translate).
  - High-contrast/dark mode; focus ring; aria labels.
 
## 9) Capabilities & Security
- `src-tauri/capabilities/default.json` allowlist:
  - Commands: `open_xml`, `start_translate`, `cancel_translate`, `save_xml`, `get_glossary`, `upsert_glossary`, `get_settings`, `update_settings`, `set_api_key_once`
  - Events: `row_translated`, `job_progress`, `job_error`, `job_done`
- Plugins: dialog, store, stronghold. No arbitrary network beyond Gemini endpoint used.
- Logging: redact sensitive fields; never log `src`/`dst` text if privacy mode is enabled (optional setting).
 
## 10) Test Strategy (Implementation-Facing)
- Unit (Rust, TextOps)
  - Mask/Restore: simple tags, nested tags, attributes, placeholders, ICU, adjacent tokens, long text.
  - Validate: unmatched tags, ICU selector checks, placeholder count mismatches.
- Integration
  - XML round-trip: Known fixtures (incl. LegacyoftheDragonborn sample) import → export; structural diff stable (ignoring whitespace).
  - Pipeline with mocked Gemini: deterministically map inputs to outputs; assert event flow and DB updates.
- E2E
  - Synthetic 50k rows dataset; run `start_translate` with mocked Gemini; verify event throughput, UI virtualization performance (manual/automated harness).
- Perf Targets
  - Import throughput; event latency p95 < 150ms; grid scroll p95 < 16ms/frame; memory < 500MB at 50k rows.
 
## 11) Open Decisions (to confirm)
- DB interface: Proceed with `rusqlite` (native, bundled) instead of SQL plugin (keeps secrets and logic purely in Rust).
- API key input: Accept “one-shot” entry via password field in UI; immediately send to backend and zeroize JS memory; never read back.
- ICU validator depth: Start with placeholder boundary checks and selector presence; extend to full MessageFormat parsing if needed.
 
## 12) Next Steps (upon approval)
- M3 Scaffold
  - Initialize Tauri v2 + Vite project tree per stub.
  - Add Cargo and NPM deps; create capabilities; wire commands/events placeholders.
  - Land initial unit tests for TextOps.
 
Please reply “continue” to proceed with Scaffold (M3). If any changes are needed (e.g., switch to SQL plugin, stronger ICU validator, different event payloads), indicate them now.
 
