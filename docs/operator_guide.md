# Operator Guide
 
## Setup
- Windows 11 with WebView2 Runtime (included by default).
- Install app MSI (see Packaging guide). First launch initializes Stronghold (secure storage).
 
## Secrets
- Open app → click “API 키 설정” and paste your Gemini API key.
- Key is stored in Stronghold snapshot (`xtr_api_vault.hold`) under app local data dir with Argon2id KDF.
- Key is never exposed to the frontend and never logged.
 
## Settings (Sidebar → 설정)
- Concurrency: number of concurrent batches (typ. 8–12).
- Batch Size: items per request (typ. 8–16).
- RPS: requests per second baseline.
- Rate Mode: 
  - gap: fixed minimum gap between requests,
  - token bucket: bursty but bounded; set Bucket Capacity.
- Model/Temperature: passed to Gemini (if mock disabled).
- Custom Prompt: appended in `[USER]` to the base VEC prompt.
- Mock LLM: enable for offline perf tests (no real translation).
- Redact Logs: hides text previews in error events/logs.
 
## Workflow
1) Settings → 저장
2) 파일 열기 (xTranslator XML)
3) 번역 시작
4) 진행/로그 확인, 오류 클릭 시 해당 행 포커스
5) 필요 시 ‘행 편집’에서 수동 보정 → 저장
6) 저장 → XML로 내보내기
 
## Logs and Error Export
- Logs can be filtered and cleared (Sidebar).
- “오류 내보내기” exports a JSON of errors with row id, code, position hint, and current text.
 
## Performance
- Mock LLM mode: UI header shows live p50/p95 and rps metrics.
- Headless perf test: `cd xtr-gemini-translator/src-tauri && cargo test -- --ignored`.
 
## Security and Privacy
- Never paste secrets into logs or prompts.
- Use “Redact Logs” to avoid text previews in emitted error events.
- Stronghold files are stored under App Local Data; back them up securely if needed.
 
