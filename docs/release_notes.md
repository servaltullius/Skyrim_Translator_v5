# X-TR Gemini Translator (Alduin) — Release Notes
Version: 0.1.0
Date: YYYY-MM-DD
 
Highlights
- XML→DB→Translate→Restore→Validate→XML pipeline with strict tag/placeholder/ICU preservation.
- Real-time row updates (Tauri events) and virtualization for 50k+ rows.
- Stronghold-backed secret storage for Gemini API key; no frontend exposure.
- Glossary + custom prompt injection with VEC template.
- Mock LLM mode and 50k performance harness; p50/p95 metrics surfaced in UI.
 
Breaking Changes
- N/A
 
New
- Sidebar with filters, settings (concurrency/batch/RPS/rate mode/model/temperature/mock/redact), glossary editor, prompt editor, and log panel.
- Editor panel for per-row editing, error focusing, and reset-to-pending.
- Validator V2 with ICU selector validation and position hints.
 
Fixes/Improvements
- Token-bucket or gap-based pacing with jittered backoff (429/5xx aware).
- Round-trip tests for XML structure/attributes and Params equality.
 
Known Issues
- Packaging requires a valid `icons/app.ico`.
- Some ICU edge cases (deeply nested or complex formatting) may need further rule refinement.
 
