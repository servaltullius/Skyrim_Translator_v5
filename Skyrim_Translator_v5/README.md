# XtrXmlTranslator — xTranslator XML 기반 데스크톱 번역기 (.NET 8 + Avalonia)

XtrXmlTranslator는 xTranslator 등에서 추출한 XML을 태그/게임 변수 100% 보존하며 번역하는 Windows/Linux 데스크톱 앱입니다.

- 언어/런타임: .NET 8
- UI: Avalonia MVVM
- 핵심 로직(Core): SRX 문장분리, 인라인 토크나이저, 용어집(보호/치환), 검증(플레이스홀더/태그), XML 스트리밍/보안, 번역 파이프라인
- 번역 백엔드: Gemini(스트리밍 SSE) + Mock(오프라인/E2E 테스트)
- 로깅: Serilog(콘솔 + 파일)
- CI: GitHub Actions(Windows/Linux 매트릭스, NuGet 캐시+락, 커버리지 임계치 75%, 조건부 통합/벤치)

## 빠른 시작

요구 사항
- .NET 8 SDK

빌드/테스트/실행(Windows PowerShell 예시)
- 복원/빌드/테스트
  - `dotnet build XtrXmlTranslator.sln`
  - `dotnet test XtrXmlTranslator.sln`
- 앱 실행
  - `dotnet run -p XtrXmlTranslator.App/XtrXmlTranslator.App.csproj`

환경 변수(선택)
- 번역기 선택: `XTRANS_TRANSLATOR=MOCK` (기본: Gemini)
- Gemini API 키: `GEMINI_API_KEY=...` (Gemini 사용 시 필수)
- 로깅 레벨/디렉터리: `XTRANS_LOG_LEVEL=Debug`, `XTRANS_LOG_DIR=logs`
- 용어집 경계 문자 추가: `XTRANS_GLOSSARY_WORDCHARS="_-’"`

예시(Windows PowerShell)
- `setx XTRANS_TRANSLATOR MOCK`
- `setx XTRANS_LOG_LEVEL Debug`
- `setx XTRANS_GLOSSARY_WORDCHARS "_-’"`

## 구성(appsettings.json/환경변수)

설정 우선순위(앞이 더 높음):
1) 환경변수(접두사: XTRANS_)
2) Config/appsettings.json (사용자/배포 전용)
3) appsettings.json (기본값)

중첩 키는 더블 언더스코어로 구분합니다. 예: Translator:Mode -> XTRANS_Translator__Mode

- Translator 모드 전환
  - PowerShell: `$env:XTRANS_Translator__Mode = "Mock"`
  - appsettings.json:
    ```json
    {
      "Translator": { "Mode": "Mock" }
    }
    ```
  - (호환) `$env:XTRANS_TRANSLATOR = "MOCK"`

- Gemini 설정 예시(appsettings.json)
  ```json
  {
    "Gemini": {
      "Model": "gemini-2.5-flash",
      "ProjectId": "",
      "Location": "us-central1",
      "Temperature": 0.2,
      "MaxConcurrency": 4,
      "RequestsPerMinute": 10,
      "HttpTimeout": "00:01:00",
      "RetryMaxAttempts": 5,
      "RetryBaseDelay": "00:00:00.500",
      "HttpHandshakeTimeout": "00:00:10",
      "SystemInstruction": ""
    }
  }
  ```

- 비밀(Secrets) 우선순위: 환경변수 `GEMINI_API_KEY` > 파일 `Config/secrets.json`
  - 파일 형식: `{ "GeminiApiKey": "..." }`

- 환경별 설정: `DOTNET_ENVIRONMENT=Development`일 때 `appsettings.Development.json`도 로드됩니다.

## 아키텍처 개요

- Core(XtrXmlTranslator.Core)
  - Tokenizing: 인라인 태그(<...>), printf/brace 플레이스홀더, 텍스트 런 분리/재조립
  - SRX: SrxLoader(2.0 XML) → SrxCompiler(lookbehind/lookahead 변환) → SrxSegmenter(토큰 경계 존중)
  - Glossary: Aho-Corasick 기반 보호(PROTECT)·치환(ENFORCE/PREFER), 전역 longest-first 비겹침 선택, ENFORCE 우선
  - Validation: printf/brace/percent-brace/%%/XML-like 태그 개수·순서·중첩, CK 원자태그 순서
  - Translate: Gemini 스트리밍 SSE(GeminiApiClient, GeminiTranslator) + ITranslator/Factory 추상화 + MockTranslator
  - XML: XmlReader 스트리밍, XXE 차단(DtdProcessing=Prohibit, XmlResolver=null), 공백 보존
- App(XtrXmlTranslator.App)
  - MVVM: MainWindowViewModel, TranslationRowVM, 커맨드 바인딩 중심(일부 더블클릭은 VM 커맨드 호출로 위임)
  - DI: ISecretsProvider, IAppConfig, ITranslatorFactory(TranslatorFactorySelector), IFullTextDialogService
  - 로깅: Serilog 초기화(콘솔+파일), 주요 경로 구조적 로그(번역 시작/진행/완료, 검증 이슈 등)

## 번역 파이프라인(요약)

1) 원문 로드/선택 → 인라인 토큰화(텍스트/태그/플레이스홀더)
2) 텍스트 런에 용어 보호(PROTECT) 마스킹 적용(⟪Tn⟫)
3) SRX 문장 분리(토큰 경계를 유지)
4) 번역 스트리밍(onDelta, onSegmentWarning, onSegmentError 콜백)
5) 언마스크 → 용어 치환(ENFORCE/PREFER) → 검증(플레이스홀더/태그)
6) UI 상태/경고 갱신, 자동 교정(오토픽스) 제공

## SRX 프리셋

- 리소스: Core/Resources/Srx/en-default.srx, ko-default.srx (임베디드)
- SrxPresets.ForLanguage("ko-KR") → KoDefault, 그 외 EnDefault
- 규칙 교체/확장: 리소스를 갱신해 빌드하면 적용(외부 파일 로딩 방식으로 확장 가능)

## Glossary(용어집)

- 타입: PROTECT(보호), ENFORCE(강제 치환), PREFER(선호 치환)
- 보호: 텍스트 런에서 매칭 구간을 토큰(⟪Tn⟫)으로 마스킹 → 번역 → 언마스크
- 치환: 전역 longest-first 비겹침 선택, ENFORCE 우선, 경계 일치 필수(단어 경계)
- 경계 설정(옵션): `XTRANS_GLOSSARY_WORDCHARS="_-’"`와 같이 추가 단어문자 지정 가능
  - 기본값: 영숫자만 단어문자(문자/숫자 외는 경계)
  - 예: `_` 추가 시 `foo_bar`에서 `foo`는 경계 일치가 아님(치환되지 않음)

## 검증(Validation)

- printf: %s, %d, %1$s 등(%% 리터럴 포함)
- brace: {0}, {name}, {0:N2}, {name:fmt}
- percent-brace: %{token}
- XML-like 태그: 개수/순서/중첩, CK 원자태그(예: <Alias=.../>) 불변

## Mock/Gemini 번역기

- MockTranslator: 원문을 그대로 에코(오프라인·E2E 테스트에 사용)
  - `XTRANS_TRANSLATOR=MOCK`
- GeminiTranslator: SSE(alt=sse), 핸드셰이크 구간만 타임아웃/재시도(Polly), 본문 스트림은 무타임아웃
  - 백오프/지터/스로틀 지원, ITranslatorFactory로 주입/교체 가능

## 로깅

- 초기화: `XTRANS_LOG_LEVEL`, `XTRANS_LOG_DIR`
- 출력: 콘솔, `logs/xtrans-YYYYMMDD.log`(일 단위 롤링, 7개 보존)
- 주요 이벤트: 번역 시작/진행률/완료, 세그먼트 경고/오류, 검증 이슈, XML 로딩 등

## CI 파이프라인

- Windows/Linux 매트릭스 빌드/테스트
- NuGet 캐시 + packages.lock.json(locked-mode 복원)
- 커버리지 수집/리포트(ubuntu에서만), 임계치 75%
- 통합 테스트: `GEMINI_API_KEY` 시크릿이 있을 때만 실행
- 벤치마크: 커밋 메시지에 `[bench]` 포함 또는 `workflow_dispatch` 수동 실행

## 개발 팁

- 오프라인 E2E 테스트: MockTranslator로 Glossary 보호→분할→에코→언마스크→치환→검증 경로 검증
- SRX/Glossary/Validator 경계 케이스는 단위 테스트를 우선 보강
- 더블클릭 제스처는 현재 코드비하인드에서 VM 커맨드를 호출(버전/네임스페이스 이슈 회피)

## 보안

- XML 로딩에서 DTD 비허용/외부 해석 차단(XXE 방지)
- 시크릿/키(Environment 변수) 주입, 로그에 민감정보 출력 금지

## 라이선스

- TBD

