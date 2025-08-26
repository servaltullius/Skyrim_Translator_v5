아래는 지금까지 합의된 요구와 기술적 제약을 바탕으로 정리한 **제품 요구사항 문서(PRD)** 초안입니다. (Windows 11 / Tauri + React / Gemini 1.5 Flash / xTranslator XML 대상)

---

## 1. 제품 개요

**제품명(가칭)**

* **X-TR Gemini Translator** (내부 코드명: *Alduin*)

**목표**

* xTranslator에서 추출한 Bethesda 게임용 XML을 **실시간 항목 단위**로 고속 번역하고, LLM에 **태그/변수/정규식**을 노출하지 않은 상태에서 번역한 뒤 **정확히 복원**한다.
* 개인용이지만 **전문 번역 워크플로**(용어집, 커스텀 프롬프트, 검증/로그)를 갖춘 **경량 데스크톱** 도구.

**플랫폼 / 기술 스택**

* **Windows 11 데스크톱**
* **Tauri(Rust) + React(TypeScript)**
* **Gemini 1.5 Flash API** 사용(가성비/속도 요건) ([Google Cloud][1])
* XML 파싱: `quick-xml`(스트리밍) 또는 `roxmltree`(읽기 전용 트리) ([Docs.rs][2])
* Diff 기반 태그 복원: `similar`(Patience/Myers) ([Docs.rs][3], [GitHub][4])

**대상 파일(예시)**

* 사용자가 제공한 `LegacyoftheDragonborn_english_korean.xml`과 유사한 **xTranslator XML 구조**를 1급 시민으로 지원한다(예: `<SSTXMLRessources><Params>…</Params><Content>…</Content></SSTXMLRessources>` 및 `<String>` 항목의 원문/번역 필드와 메타).

---

## 2. 페르소나 & 핵심 시나리오

**페르소나**

* 모더/번역자(개인): 대형 모드 텍스트를 빠르게 초기 번역 + 사후 교정.
* 테크니컬 모더: 태그/플레이스홀더/게임 변수 무결성에 민감.

**핵심 시나리오**

1. xTranslator XML 불러오기 → 항목 리스트가 로드됨 → **번역 시작** 클릭 → 항목별 번역이 **완료되는 즉시 UI에 반영**.
2. 용어집(JSON)을 켜고 **일관 번역** 유지(LLM 지침 + 사후치환).
3. 초장문 항목은 스트리밍/분할로 처리, 전체 작업은 **동시 처리**로 속도 최적화. ([Google Cloud][5])

---

## 3. 범위(Scope)

**포함**

* xTranslator XML → 내부 DB 적재 → **마스킹 → 번역 → 태그/변수 복원 → 저장** 자동 파이프라인.
* 좌측 사이드바(파일/상태필터/용어집/프롬프트/설정), 중앙 **원문/번역/메타 그리드**, 하단 로그.
* Glossary(JSON) 관리 UI, 커스텀 프롬프트(VEC 기본 + 사용자 추가).
* **실시간 이벤트** 기반 행 단위 갱신(백엔드 emit → 프론트 listen). ([Tauri][6], [Docs.rs][7])

**비포함(초기 버전)**

* 다국어 다중 타깃(ko 이외), 협업 서버/클라우드 동기화, TM/메모리, 댓글/리뷰 워크플로.

---

## 4. 기능 요구사항(FRs)

### FR1. XML 입출력

* FR1.1 XML 열기: xTranslator XML을 파싱해 항목(row) 단위로 메모리/SQLite에 적재. 스트리밍 파싱 옵션 제공. ([Docs.rs][2])
* FR1.2 XML 저장(내보내기): 번역 완료본을 원본 구조/인코딩 유지하여 재생성.
* FR1.3 구조 검증: 필수 노드/속성, 태그 짝·중첩·순서 유효성 검사.

### FR2. 번역 파이프라인 (완전 자동, 항목 단위 실시간)

* FR2.1 **마스킹**: `<…>` 태그, `{변수}`, `%s`류 플레이스홀더, ICU MessageFormat 요소 등 비번역 토큰을 보호용 플레이스홀더로 교체. ([unicode-org.github.io][8], [help.smartling.com][9])
* FR2.2 **LLM 번역**: 순수 텍스트만 **Gemini 1.5 Flash**에 전달(REST/SDK), `generateContent` 또는 스트리밍 사용. ([Google AI for Developers][10], [Google Cloud][5])
* FR2.3 **태그 복원**: 원문/번역문 텍스트 Diff(예: Patience)로 위치를 재추정하고 태그/플레이스홀더를 정확히 복원. ([Docs.rs][3])
* FR2.4 **검증/정규화**: 복원 후 태그 짝·중첩·순서/ICU placeholder 검증. ([unicode-org.github.io][8])
* FR2.5 **실시간 UI 업데이트**: 각 항목이 완료될 때마다 이벤트로 **행 단위 업데이트**. ([Tauri][6])

### FR3. 용어집(Glossary)

* FR3.1 JSON 기반 키-값(`"Dragonborn": "드래곤본"`) 관리 UI(추가/편집/검색/중복검사).
* FR3.2 적용 방식: (a) 번역 지침에 **동적 주입**(핵심 용어만) + (b) **사후 치환**/불일치 경고.
* FR3.3 개념적 정합성: 용어집은 **일관 번역 유지용 사전**이라는 업계 관행과 일치(Cloud Translation의 Glossary 개념 준용). ([Google Cloud][11])

### FR4. 커스텀 프롬프트

* FR4.1 기본 프롬프트(VEC) + 사용자 추가 텍스트(길이 제한/가이드 표시).
* FR4.2 프로젝트별 프롬프트 프로필 저장/불러오기.

### FR5. UI/UX

* FR5.1 **레이아웃**: 좌측 사이드바(파일/작업/필터/용어집/프롬프트/설정), 중앙 **DataGrid(원문/번역/메타)**, 하단 로그/진행률.
* FR5.2 **가상 스크롤**: 수천\~수만 행에서도 부드러운 스크롤(TanStack Table + react-window/virtual). ([TanStack][12], [GitHub][13])
* FR5.3 **상태 필터**: 미번역/완료/오류/검토 필요.
* FR5.4 **편집 UX**: 번역문 셀 인라인 편집, 용어 미일치/태그 경고 하이라이트.
* FR5.5 **검색/치환**: ID/원문/번역/메타 검색.
* FR5.6 다크 모드, 단축키(저장/실행/토글), 접근성(포커스/읽기 순서).

### FR6. 성능/안정성

* FR6.1 동시 처리: 백엔드 워커 풀 + **Tokio Semaphore**로 동시 요청 수 제어(레이트리밋·백오프). ([Docs.rs][14])
* FR6.2 HTTP 클라이언트: `reqwest::Client` **커넥션 풀 재사용**. ([Docs.rs][15])
* FR6.3 배치 번역: 짧은 항목 다건을 한 요청으로 묶어 오버헤드 감소.
* FR6.4 초장문: UAX #29 기준 문장/단락 분할 + 스트리밍 표시. ([Unicode][16])

### FR7. 로깅/진행률/오류

* FR7.1 진행률: 전체/남은/분당 처리량 표시.
* FR7.2 오류 재시도: 지수 백오프/부분 재시도, 실패만 재시도 기능.
* FR7.3 감사 로그: 요청/응답 메타(토큰/시간) 저장(민감 텍스트 미저장 옵션).

---

## 5. 비기능 요구사항(NFRs)

* **성능**

  * 평균 항목(50\~200자) 처리 완료 → \*\*실시간(≤1.5초 p50, ≤3초 p95)\*\*로 UI 반영(10개 동시 처리 기준).
  * 1회 세션에서 **≥50k 행**을 가상 스크롤로 원활히 탐색 가능(메모리 누수 없이). ([GitHub][13])
* **정확성**

  * 태그/변수/ICU 자리표시자 **100% 보존**(짝/중첩/순서). ([unicode-org.github.io][8])
* **안정성**

  * 네트워크 오류/429에서 자동 재시도, 중단 후 **재개 가능**.
* **보안**

  * API 키는 **백엔드에만 저장/사용**, Stronghold 등 안전 저장 적용. ([Tauri][17])
* **호환성/배포**

  * Windows 11 + WebView2(Win11은 기본 탑재) / MSI 번들 배포. ([Tauri][18], [Microsoft Learn][19])

---

## 6. 아키텍처(요약)

```
[React UI]  ──invoke──▶  [Tauri/Rust Commands]
  ▲   │                    │
  │   └──listen(row_translated/job_progress) ◀── emit ── 워커/큐
  │
  └── DataGrid(가상 스크롤): 행 상태 실시간 반영
```

* **IPC**: Commands(요청) + Events(실시간 갱신) 조합은 Tauri의 권장 패턴. ([Tauri][6])
* **파이프라인**: XML 파싱(quick-xml/roxmltree) → 마스킹 → Gemini 번역 → Diff 복원(similar) → 검증 → 저장. ([Docs.rs][2])
* **데이터 계층**: SQLite(`tauri-plugin-sql`)로 항목/메타/작업상태 관리. ([Tauri][20])

---

## 7. 세부 설계 규격

### 7.1 Tauri Commands (예시 사양)

* `open_xml(path: string) -> {count: number, meta: {...}}`
* `start_translate(opts: {concurrency?: number, batchSize?: number}) -> {startedAt}`
* `cancel_translate() -> {canceled: boolean}`
* `save_xml(path?: string) -> {written: number}`
* `get_glossary() -> Glossary` / `upsert_glossary(g: Glossary)`
* `get_settings()` / `update_settings(patch)`
* 이벤트: `row_translated {id, dst, status}`, `job_progress {done,total,rate}`, `job_error {id, code, message}` (프론트는 `listen/listen_any`로 구독). ([Tauri][21], [Docs.rs][7])

### 7.2 데이터 모델 (SQLite)

* `entries(id, edid, rec, src, dst, status, meta_json, updated_at)`
* 인덱스: `(status)`, `(edid)`, 필요 시 FTS5(src,dst).
* `glossary(key TEXT PRIMARY KEY, value TEXT, note TEXT)`
* `settings(key PRIMARY KEY, value JSON)`

### 7.3 마스킹/복원 규칙

* **마스킹 대상**: XML 태그, `{name}`, `%s/%d`, `$1` 등 포맷토큰, ICU `{var, plural, …}`. ([unicode-org.github.io][8])
* **복원 알고리즘**: TextDiff(Patience) → Equal/Replace/Insert/Delete 핸들링 → 경계 재조정 → 검증. ([Docs.rs][3])
* **문장 분할**: UAX #29(문장 경계) 기반 분할/재조합. ([Unicode][16])

### 7.4 Gemini 1.5 Flash 연동

* REST: `POST /v1beta/models/gemini-1.5-flash:generateContent` (Google AI for Developers) 또는 Vertex AI 엔드포인트 사용. ([Google AI for Developers][22], [Google Cloud][5])
* 모델 특성: 장문 컨텍스트(최대 입력 토큰 등)로 대형 텍스트 처리 적합. ([Google Cloud][1])
* 스트리밍: 초장문 항목은 `streamGenerateContent`로 점진 수신. ([Google Cloud][5])
* 클라이언트: `reqwest` 재사용/타임아웃/재시도, 동시성은 `tokio::sync::Semaphore`. ([Docs.rs][15])

### 7.5 UI 컴포넌트

* 테이블: TanStack Table(헤드리스) + react-window/virtual(행/열 가상화) ([TanStack][12], [GitHub][13])
* 파일/다이얼로그/FS/Store/SQL: 공식 Tauri 플러그인 사용(권한/스코프 설정). ([Tauri][23])

### 7.6 보안/배포

* API 키/민감 설정: Stronghold 플러그인 저장, 프론트 미노출. ([Tauri][17])
* Win11 WebView2: 기본 탑재, 구버전 Windows는 인스톨러에서 Runtime 확인/설치. ([Tauri][18], [Microsoft Learn][19])
* Windows 빌드/배포: Tauri CLI, MSI 생성. ([Tauri][24])
* 개발 머신 전제: MSVC Build Tools + WebView2(개발/테스트). ([Tauri][25])

---

## 8. 성능 목표 & 벤치마크 플랜

* **지연 시간**: 항목 단위 p50 ≤1.5s, p95 ≤3s(50–200자, 동시 10).
* **처리량**: 10분 내 **≥15k 행** 번역(네트워크/쿼터 환경에 의존).
* **UI 성능**: 50k+ 행 스크롤 시 프레임 드롭 없이 상호작용(react-window 가상화 증빙). ([GitHub][13])
* **복원 정확도**: 홀딩 아웃 테스트에서 태그/변수/ICU 위반 0건. ([unicode-org.github.io][8])

---

## 9. 품질 보증(테스트)

* **유닛**:

  * 마스킹/복원 라운드트립(태그/ICU/플레이스홀더/중첩/인접/경계).
  * Diff 복원 경계 케이스(Equal/Replace/Insert/Delete). ([Docs.rs][3])
* **통합**:

  * XML→DB→번역→XML 전과정 스냅샷 비교.
  * 네트워크 오류/429/타임아웃 재시도 시퀀스.
* **E2E**:

  * 대용량 파일 스트레스(수십 MB, 수만 행). quick-xml 스트리밍 확인. ([Docs.rs][2])
* **사용성**:

  * 실시간 행 갱신 이벤트 누락/중복 검증(Tauri events). ([Docs.rs][7])

---

## 10. 수용 기준(Acceptance Criteria)

1. `LegacyoftheDragonborn_english_korean.xml`(샘플) 로드 후, **번역 시작** 시 **행 단위**로 번역 결과가 즉시 테이블에 반영된다(스피너→확정).
2. `<태그>`/`{변수}`/ICU placeholder가 **손실 없이 동일 위치/관계**로 복원된다(자동 검증 통과). ([unicode-org.github.io][8])
3. 용어집에 등록한 용어는 번역 결과에 **일관 적용**된다(LLM 주입 + 사후치환). ([Google Cloud][11])
4. 5만+ 행 목록에서 스크롤/검색/편집 시 **UX 렉 없음**(가상화 적용). ([TanStack][12], [GitHub][13])
5. API 키는 프론트에 노출되지 않으며 Stronghold 저장을 통과한다. ([Tauri][17])

---

## 11. 리스크 & 대응

* **LLM 출력 변동성** → temperature 낮춤(0–0.2), 용어집 사후치환, Diff 복원.
* **레이트리밋/네트워크** → 세마포어 동시성 상한, 지수 백오프/재시도. ([Docs.rs][14])
* **대용량 메모리 압박** → quick-xml 스트리밍, React 가상화. ([Docs.rs][2], [GitHub][13])
* **WebView2 의존** → Win11 기본 포함 + 인스톨러에서 확인/설치. ([Tauri][18], [Microsoft Learn][19])

---

## 12. 오픈 이슈(내부 검토 항목)

* xTranslator XML 변형 스키마(모드별 커스텀 태그) 대응 규칙 구체화.
* 커스텀 프롬프트 길이 상한(토큰/비용/품질 트레이드오프) 가이드.
* 프로젝트별 프리셋(게임/모드) 관리 UX.
* Glossary 범위(문장 경계/부분 단어/대소문자/복수형) 규칙.

---

## 13. 참고 근거(핵심)

* **Tauri IPC(Commands/Events)**: 이벤트 수신/송신 패턴(emit/listen\_any) ([Tauri][6], [Docs.rs][7])
* **XML 파싱**: quick-xml(스트리밍), roxmltree(읽기 트리) ([Docs.rs][2])
* **Diff 복원**: `similar`(Patience/Myers) ([Docs.rs][3], [GitHub][4])
* **가상 스크롤**: TanStack Table + react-window/virtual 가이드/예제 ([TanStack][12], [GitHub][13])
* **Gemini API**: generateContent/streamGenerateContent, 1.5 Flash 모델 특성/토큰 한도 ([Google AI for Developers][10], [Google Cloud][5])
* **UAX #29 / ICU**: 문장 경계/메시지 포맷 가이드 ([Unicode][16], [unicode-org.github.io][8])
* **Glossary 개념**: Cloud Translation Glossary(일관 번역) ([Google Cloud][11])
* **Windows 전제/배포**: MSVC Build Tools, WebView2(Win11 기본, 런타임 배포), 빌드/인스톨러 ([Tauri][25])

---

## 14. 마일스톤(제안)

* **MVP**: XML→DB 로드/저장, 자동 번역 파이프라인(마스킹/복원), 실시간 행 업데이트, 용어집/프롬프트 기본, 가상 스크롤.
* **Beta**: Glossary 고급 규칙, 고급 검증(태그/ICU), 스트리밍 UI, 오류 재시도/재개, 로그/성능 메트릭.
* **1.0**: 설치/업데이트 패키징, 크래시/진단 리포트(옵트인), 키보드 단축키/접근성.

---

### 부록 A. UI 와이어 서술(간략)

* **좌측 사이드바**:

  * 섹션: *파일*, *번역*, *필터(미번역/완료/오류)*, *용어집*, *프롬프트*, *설정*.
* **상단 툴바**: *열기/저장*, *번역 시작/중지*, *실패 재시도*, *검색*.
* **중앙 DataGrid**: 컬럼 = *ID | 상태 | 유형 | 원문 | 번역 | 길이 | 경고(태그/용어)*. 행 클릭 시 하단 에디터 패널 확장.
* **하단 로그/진행률**: ETA, rps, 오류 상세.

---

필요하시면 이 PRD를 기준으로 **프로젝트 스캐폴딩(폴더/설정), 핵심 Tauri Command/Event 시그니처, DB 스키마 SQL, 마스킹·복원 유닛테스트 케이스 목록**까지 바로 정리해 드리겠습니다.

[1]: https://cloud.google.com/vertex-ai/generative-ai/docs/models/gemini/1-5-flash?utm_source=chatgpt.com "Gemini 1.5 Flash | Generative AI on Vertex AI"
[2]: https://docs.rs/quick-xml?utm_source=chatgpt.com "quick_xml - Rust"
[3]: https://docs.rs/similar?utm_source=chatgpt.com "similar - Rust"
[4]: https://github.com/mitsuhiko/similar?utm_source=chatgpt.com "mitsuhiko/similar: A high level diffing library for rust based ..."
[5]: https://cloud.google.com/vertex-ai/generative-ai/docs/model-reference/inference?utm_source=chatgpt.com "Generate content with the Gemini API in Vertex AI"
[6]: https://v2.tauri.app/concept/inter-process-communication/?utm_source=chatgpt.com "Inter-Process Communication"
[7]: https://docs.rs/tauri/latest/tauri/trait.Listener.html?utm_source=chatgpt.com "Listener in tauri - Rust"
[8]: https://unicode-org.github.io/icu/userguide/format_parse/messages/?utm_source=chatgpt.com "Formatting Messages | ICU Documentation"
[9]: https://help.smartling.com/hc/en-us/articles/360008030994-ICU-MessageFormat?utm_source=chatgpt.com "ICU MessageFormat"
[10]: https://ai.google.dev/api/generate-content?utm_source=chatgpt.com "Generating content | Gemini API | Google AI for Developers"
[11]: https://cloud.google.com/translate/docs/advanced/glossary?utm_source=chatgpt.com "Creating and using glossaries (Advanced)"
[12]: https://tanstack.com/table/v8/docs/guide/virtualization?utm_source=chatgpt.com "Virtualization Guide | TanStack Table Docs"
[13]: https://github.com/bvaughn/react-window?utm_source=chatgpt.com "bvaughn/react-window: React components for efficiently ..."
[14]: https://docs.rs/tokio/latest/tokio/sync/struct.Semaphore.html?utm_source=chatgpt.com "Semaphore in tokio::sync - Rust"
[15]: https://docs.rs/reqwest/latest/reqwest/struct.Client.html?utm_source=chatgpt.com "Client in reqwest - Rust"
[16]: https://unicode.org/reports/tr29/?utm_source=chatgpt.com "UAX #29: Unicode Text Segmentation"
[17]: https://v2.tauri.app/plugin/stronghold/?utm_source=chatgpt.com "Stronghold"
[18]: https://v2.tauri.app/reference/webview-versions/?utm_source=chatgpt.com "Webview Versions"
[19]: https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution?utm_source=chatgpt.com "Distribute your app and the WebView2 Runtime"
[20]: https://v2.tauri.app/plugin/sql/?utm_source=chatgpt.com "SQL"
[21]: https://v2.tauri.app/develop/calling-frontend/?utm_source=chatgpt.com "Calling the Frontend from Rust"
[22]: https://ai.google.dev/gemini-api/docs?utm_source=chatgpt.com "Gemini API | Google AI for Developers"
[23]: https://v2.tauri.app/plugin/dialog/?utm_source=chatgpt.com "Dialog"
[24]: https://v2.tauri.app/distribute/windows-installer/?utm_source=chatgpt.com "Windows Installer"
[25]: https://v2.tauri.app/start/prerequisites/?utm_source=chatgpt.com "Prerequisites"
