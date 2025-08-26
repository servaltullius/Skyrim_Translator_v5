# Plan Review: X-TR Gemini Translator (Alduin)
Version: 0.1.0
Owner: Alduin Builder
Scope Basis: AGENTS.md (governance), PRD.md (features), stub.md (scaffold)
Determinism: default temperature 0.2 unless specified
Security: No secrets in frontend; Stronghold for API keys
Review Gate: Await explicit “continue” from caller after this plan
 
## Acceptance Criteria Mapping (DoD)
- XML→DB→(mask→translate via Gemini API→diff-restore→validate)→save pipeline: Implemented in Rust backend with `quick-xml`, `rusqlite`, `similar`, `regex`, `reqwest`, validation rules; round-trip tests pass.
- Real-time row updates via Tauri events: `row_translated`, `job_progress`, `job_error`, `job_done` emitted; frontend listeners update grid in real-time.
- 50k+ rows virtualization: TanStack Table + react-window with overscan; scroll perf p95 < 16 ms/frame; memory stable.
- Glossary and custom prompts: editable glossary in DB; VEC prompt template merged at runtime; user prompts supported.
- Strict preservation of tags/placeholders/ICU: masking + restore + validator; unit tests cover tags, placeholders, ICU, malformed cases.
- Security: Stronghold stores secrets; API key never exposed to frontend; capabilities-permission limited; no arbitrary network outside Gemini endpoint.
 
## Work Breakdown Structure (WBS)
1. Planning
   1.1 Establish acceptance criteria and DoD
   1.2 Define milestones and risks
   1.3 Test & perf targets
2. Design
   2.1 Backend architecture (commands/events, pipeline, storage, secrets)
   2.2 XML schema analysis (xTranslator) and mapping to DB
   2.3 Mask/restore/validate rules (ICU, placeholders, XML)
   2.4 Frontend architecture (state, virtualization, panels)
3. Scaffold
   3.1 Initialize Tauri v2 + Vite React TS project
   3.2 Add Cargo deps; tauri.conf.json; capabilities; plugins
   3.3 Frontend deps (TanStack Table, react-window, Zustand)
   3.4 Basic project scripts and lint/format config
4. Backend Implementation
   4.1 DB schema and repository functions (rusqlite)
   4.2 XML streaming loader/saver (quick-xml)
   4.3 TextOps: mask/restore/refine/validate with tests
   4.4 LLM client (Gemini REST) + Stronghold key access
   4.5 Concurrency pipeline (Semaphore, batching, retries)
   4.6 Tauri commands + event emission
5. Frontend Implementation
   5.1 Tauri API wrappers (invoke/listen)
   5.2 Virtualized grid (TanStack + react-window)
   5.3 Sidebar: file open/save, controls, filters
   5.4 Glossary editor + prompt panel
   5.5 Progress/log panel; a11y + dark mode polish
6. Testing & Benchmarking
   6.1 Unit: mask/restore/validate
   6.2 Integration: XML→DB→XML round-trip
   6.3 E2E: pipeline on large file (50k+ synthetic)
   6.4 Perf: p50/p95 latency, memory, FPS under virtualization
7. Packaging & Release
   7.1 WebView2 runtime check
   7.2 MSI bundle with resources
   7.3 Stronghold initialization and migration script
8. Review & Handover
   8.1 Code review and docs
   8.2 Operator guide (key management, rate limits)
 
## Milestones & Deliverables
- M1 Plan (this document)
  - WBS, milestones, risks, test strategy, DoD mapping
- M2 Design
  - architecture.md: IPC, events, threading, DB ERD; textops rulebook; prompt spec; security model (capabilities/permissions)
- M3 Scaffold
  - Compilable Tauri+React skeleton; configs; stubs wired; CI script (optional)
- M4 Backend Core
  - XML I/O implemented; DB ops; textops tested; pipeline baseline; events wired
- M5 Frontend Core
  - Virtualized grid, sidebar, glossary/prompt panels, live updates
- M6 Tests & Perf
  - Unit/integration/E2E suites; perf report (p50/p95); 50k rows verified
- M7 Package
  - MSI artifact; WebView2 check; Stronghold flow; release notes
 
## Key Risks & Mitigations
- Tag/placeholder/ICU preservation
  - Mitigation: robust masking regex sets; ICU-aware patterns; diff-based refine; comprehensive tests (nested, adjacent, malformed)
- Gemini API limits/quality variance
  - Mitigation: batching, concurrency caps via Semaphore; retries/backoff; deterministic settings (temp 0.2); glossary-instructed prompts
- Large XML memory/throughput
  - Mitigation: streaming parse/write; chunked DB inserts; WAL mode; indices; selective pagination in UI
- UI performance (50k+ rows)
  - Mitigation: react-window virtualization; memoization; overscan tuning; batched state updates via Zustand
- Security of API key
  - Mitigation: Stronghold + capabilities; no exposure to frontend; never log secrets
- Windows packaging quirks
  - Mitigation: WebView2 runtime detection; MSI resources; code signing plan (if provided later)
 
## Test Strategy
- Unit (Rust):
  - textops::mask: various tag types, placeholders, ICU, long texts
  - textops::restore/refine: order/adjacency, diff stabilization
  - validate: unmatched tags, ICU braces; returns actionable errors
- Integration:
  - XML→DB→XML round-trip with fixtures (incl. LegacyoftheDragonborn sample)
  - Pipeline happy path with mocked Gemini client
- E2E:
  - Import large synthetic 50k rows; start_translate; verify events; measure throughput
  - Save XML; external validator compare shape/placeholder integrity
- Performance Targets:
  - Import: ≥ 20k rows/min on dev hardware (tunable)
  - Translate loop: event latency p95 < 150ms/update
  - Grid scroll: p95 < 16ms/frame, memory < 500MB at 50k
 
## Operational Notes
- Default concurrency 8–12; auto-backoff knobs
- Stronghold initialization on first run; dedicated salt file under app data dir
- Logging excludes sensitive payloads; redact tokens
- Prompt composition: VEC base + glossary injection + custom user prompt
 
## Next Step (Review Gate)
Please review and reply “continue” to proceed to M2 Design. If you want adjustments (e.g., stricter ICU validator scope, different perf targets, or packaging choices), indicate here before we scaffold.
 
