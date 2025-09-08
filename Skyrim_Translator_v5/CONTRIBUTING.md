# Contributing to XtrXmlTranslator

감사합니다! 아래 가이드는 이 레포지토리에 기여하기 위한 빠른 안내서입니다.

## 개발 환경
- .NET 8 SDK
- Windows/Linux (Avalonia 지원)

## 브랜치/PR
- PR 단위는 작게 유지(기능/리팩토링/문서 등 주제별)
- PR 설명에 변경 요약/테스트 결과/리스크 포함

## 빌드/테스트
- 복원/빌드: `dotnet build XtrXmlTranslator.sln`
- 테스트: `dotnet test XtrXmlTranslator.sln`
  - 로컬 커버리지는 선택사항, CI에서 수집(ubuntu), 임계치 75%
- E2E(오프라인): `XTRANS_TRANSLATOR=MOCK` 설정 후 시나리오 테스트 가능

## 실행
- `dotnet run -p XtrXmlTranslator.App/XtrXmlTranslator.App.csproj`
- 번역기 선택: `XTRANS_TRANSLATOR=MOCK|GEMINI` (기본 GEMINI)
- Gemini 사용 시 `GEMINI_API_KEY` 필요

## 환경 변수
- `GEMINI_API_KEY`: Gemini API 키(필요 시)
- `XTRANS_TRANSLATOR`: MOCK/GEMINI
- `XTRANS_LOG_LEVEL`: Verbose/Debug/Information/Warning/Error/Fatal (기본 Information)
- `XTRANS_LOG_DIR`: 로그 디렉터리(기본 logs)
- `XTRANS_GLOSSARY_WORDCHARS`: 추가 단어문자 집합 예) `"_-’"`

## 코드 지침
- 코어/앱 경계를 유지: Core는 App에 의존하지 않음
- SRX/Glossary/Validator/Translate/Xml 등 모듈별 책임을 분명히
- UI는 MVVM, 가능한 커맨드 바인딩 사용(더블클릭은 VM 커맨드 호출로 위임)
- 네임드 서비스/팩토리(ITranslatorFactory, ISecretsProvider, IAppConfig, IFullTextDialogService) 주입 사용
- 로그는 Serilog로 구조적 데이터 사용(Log.Information/Warning/Error 등)

## 테스트 작성
- Glossary/SRX/Validator 경계 케이스 최우선
- E2E(Mock)로 보호→분할→에코→언마스크→치환→검증 경로 검증
- Snapshot 테스트는 Verify 기반을 권장(이미 도입됨)

## CI
- Windows/Linux 매트릭스, NuGet 캐시 + locked-mode
- 커버리지(ubuntu): 75% 미만은 실패 처리
- 통합 테스트: `GEMINI_API_KEY` 존재 시 작동
- 벤치마크: 커밋 메시지 `[bench]` 또는 수동 실행(workflow_dispatch)

## 보안
- XML 로딩은 XXE 차단 구성 사용(DTD 금지, XmlResolver=null)
- 시크릿은 환경 변수 주입, 로그 출력 금지

## 문의
- 이슈/PR로 의견/제안 남겨주세요.

