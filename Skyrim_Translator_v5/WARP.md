Rule: xTranslator XML 기반 데스크톱 번역툴 구현 규칙
# XTranslator XML → Gemini 번역기 — 영구 규칙(Rulebook)

## 1) 보안·파싱
- MUST  XmlReader 스트리밍 파싱 사용. `DtdProcessing=Prohibit`, `XmlResolver=null`로 XXE 차단.
- MUST  모든 입력 XML은 BOM/인코딩을 그대로 유지하고, DOM 로드(전체 메모리) 금지.
- MUST  `xml:space="preserve"`의 상속 의미를 준수, 공백/개행/탭/말줄임 모두 원형 유지.

## 2) 세그먼트·토큰화
- MUST  SRX 2.0 규칙(ko/en/ja/zh)로 분할. 태그 경계에는 절대 분할 금지.
- SHOULD UAX #29 문장 경계 가이드를 보조 규칙으로 채택(약어/괄호/인용부호 예외).

## 3) 인라인 태그/코드 & 자리표시자
- MUST  번역 전후 `<ph>/<pc>/<sc>/<ec>` **개수·짝·순서** 동등성 확보. 불일치 시 실패 처리.
- MUST  플레이스홀더 원형 보존: `printf` 전 구성요소(플래그/폭/정밀도/길이/포지셔널) + .NET Composite `{idx|name:format}` + `%{var}`.
- SHOULD 중괄호 이스케이프(`{{`, `}}`) 및 `%%` 처리 회귀 테스트를 유지.

## 4) 용어집/마스킹
- MUST  용어 **PROTECT(강보호)** = 마스킹 → 번역 → 원복. **PREFER(권고)** = 프롬프트 주입 + 위반 감지.
- SHOULD Aho–Corasick 기반 다중 패턴 매칭으로 대량 용어 처리.

## 5) 모델 어댑터(Gemini)
- MUST  `streamGenerateContent?alt=sse`로 SSE 수신. 응답 `Content-Type`이 `text/event-stream`이면 SSE 파서, 아니면 JSON 파서로 폴백.
- MUST  실패 시 지수 백오프(재시도), 429면 동시성·청킹 감소 후 재시도.
- SHOULD 시스템 인스트럭션 = 기본(vec) + 유저 커스텀 + 용어집 요약(권고어휘), `temperature=0~0.3`.

## 6) 회복탄력성·요율
- MUST  HttpClientFactory + Resilience(재시도/타임아웃/회로차단/속도제한) 표준 파이프라인 사용.
- MUST  RPM/TPM/RPD를 **프로젝트 단위**로 관리. 동시성 상한·분할 크기·백오프는 설정파일로 노출.

## 7) 리어셈블·불변성 검증
- MUST  태그·자리표시자 **카운트/순서/타입** 일치 검사(불일치 시 자동 수정 금지, 사용자 경고).
- MUST  `xml:space`/개행(CRLF/LF) 보존 스냅샷 테스트 100% 통과.

## 8) UI/UX
- MUST  Avalonia Fluent 다크 테마 + DataGrid/TreeDataGrid 스타일 포함.
- MUST  스트리밍 델타는 UI 스레드(Dispatcher.UIThread)로 누적 갱신.
- SHOULD 대용량에 대비하여 가상화/지연측정(프레임/할당) 자동 점검.

## 9) 테스트·CI
- MUST  단위(Tokenizer/Segmenter/Reassembler), 스냅샷(Verify), Property(FsCheck), Headless UI 테스트를 CI에 포함.
- SHOULD GitHub Actions `setup-dotnet` 캐시 + `packages.lock.json`로 재현성 유지.

## 10) 게임(베데스다) 특화
- MUST  `<Alias=>/<Global=>/<BaseName>` 등 CK 텍스트 치환 토큰은 모델 입력에서 비노출·원형 보존.
- SHOULD Message 포맷(`%.0f` 등) 시 자리표시자 개수/순서 불변성 검사.

---