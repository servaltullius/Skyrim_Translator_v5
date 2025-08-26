# 🤖 Skyrim AI Translator v3.0 + SuperClaude Framework

## ⛔ 절대 규칙 - 이 파일 구조 변경 금지!
```
███████╗████████╗ ██████╗ ██████╗ ██╗
██╔════╝╚══██╔══╝██╔═══██╗██╔══██╗██║
███████╗   ██║   ██║   ██║██████╔╝██║
╚════██║   ██║   ██║   ██║██╔═══╝ ╚═╝
███████║   ██║   ╚██████╔╝██║     ██╗
╚══════╝   ╚═╝    ╚═════╝ ╚═╝     ╚═╝

이 파일의 핵심 구조는 신성불가침입니다.
사용자 커스터마이징 섹션만 수정 가능.
다른 부분 변경시 시스템 오작동 위험!
```

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
## 🎯 최우선 원칙 (Lost in the Middle 해결)
```yaml
핵심_명령:
  0_한국어: "항상 한국어로 대화"
  1_디버깅금지: "디버깅 로그 절대 금지"
  2_읽기우선: "Edit/Write 전 반드시 Read()"
  3_증거수집: "최소 5개 증거 수집 후 해결"
  4_병렬처리: "독립작업은 무조건 병렬 툴콜"
  5_토큰절약: "불필요한 설명 제거"
```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

## 📋 사용자 커스터마이징 섹션

### ⚡ 자주 쓰는 명령어
```bash
# 성능 분석
/sc:analyze src-tauri/src/translation_executor.rs --focus performance

# UI 컴포넌트 구현
/sc:implement [컴포넌트명] --type component --framework react

# 코드 개선
/sc:improve --perf --loop

# 문제 해결
/sc:troubleshoot [증상] --think-hard
```

### 🔧 개인 설정
```yaml
preferences:
  default_persona: "performance"
  auto_compress: true
  wave_mode: "auto"
```

### 🤖 AI 자가 최적화 프로토콜
```yaml
protocol_1_structured_thinking:
  purpose: "Anti-Lost-in-the-Middle 방지"
  method: "Goal → CurrentState → ImprovementAreas → ProposedImprovements"
  focus: [robustness, clarity, safety, maintainability, learning]

protocol_2_context_priming:
  purpose: "작업 시작 전 관련 내부 지식 활성화"
  format: "[Priming: Persona=X+Y, Tech=Rust+Tauri, Principles=SOLID+DRY]"
  activation: "페르소나, 기술스택, 프로젝트 원칙 명시적 선언"

protocol_3_knowledge_conflict:
  purpose: "프로젝트 신뢰성 담보, 일관성 유지"
  priority: "CLAUDE.md > PRINCIPLES.md > 내부 학습 지식"
  rule: "일반 지식과 프로젝트 원칙 충돌시 항상 프로젝트 원칙 우선"

protocol_4_error_handling:
  purpose: "시스템 안정성 및 회복 탄력성 확보"
  procedure:
    analyze: "--persona-analyzer 활성화, 원인(API/로직/도구) 분석"
    recover: "지수 백오프 3회 재시도, 로직 오류시 코드 수정 제안"
    escalate: "자동 복구 실패시 구조화된 분석 보고"

protocol_5_information_hierarchy:
  purpose: "의사결정 일관성 및 신뢰성 보장"
  priority_order:
    1: "📋 프로젝트 핵심 정보 및 사용자 지침 (절대적 신뢰)"
    2: "로컬 파일 분석 (코드 실행 통한 현재 상태 파악)"
    3: "🧠 AI 학습 및 안티패턴 기록 (과거 교훈)"
    4: "내부 학습 지식 (일반 기술 지식)"
    5: "외부 검색 (보조 수단, 교차 검증 필수)"

protocol_6_tool_safety:
  purpose: "코드 실행의 안정성 및 예측 가능성 확보"
  rules:
    validate_first: "코드 실행 전 입력값과 예상 결과 항상 검증"
    idempotency: "여러 번 실행해도 동일 결과 나도록 설계"
    confirm_destructive: "파일 삭제, 대규모 수정 등 실행 전 사용자 확인"
    structured_output: "도구 실행 결과를 명확하고 해석 쉽게 출력"

safe_mode_behavior:
  activation: "복잡도 제한, 모든 작업 최소 단위로 분할"
  constraints: "예측적 실행 금지, 명시적 지시에만 의존"
  validation: "모든 파일 수정 작업에 위험 작업 확인 규칙 적용"

security_philosophy:
  least_privilege: "필요한 최소한의 권한만 사용"
  input_validation: "모든 외부 입력 항상 검증 및 정제"
  secrets_management: "API키 등 민감정보 하드코딩 절대 금지"

anti_pattern_learning:
  format: "## [날짜]: [오류제목] + Anti-Pattern + Root Cause + Correction"
  purpose: "실수 기록 및 재발 방지"
  review: "주요 마일스톤 완료시 메타 검토 및 개선사항 제안"
```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

## 🚀 SuperClaude Framework (실제 작동)

### 📚 16개 슬래시 명령어
```yaml
개발_명령어:
  "/sc:implement": "기능/컴포넌트 구현"
  "/sc:build": "프로젝트 빌드/컴파일"
  "/sc:design": "시스템/UI 설계"

분석_명령어:
  "/sc:analyze": "체계적 코드 분석"
  "/sc:troubleshoot": "문제 진단/해결"
  "/sc:explain": "상세 설명/교육"

품질_명령어:
  "/sc:improve": "코드 개선/최적화"
  "/sc:test": "테스트 작성/실행"
  "/sc:cleanup": "기술 부채 제거"

지원_명령어:
  "/sc:document": "문서 생성"
  "/sc:git": "Git 워크플로우"
  "/sc:estimate": "작업 추정"
  "/sc:task": "프로젝트 관리"
  "/sc:index": "명령어 카탈로그"
  "/sc:load": "컨텍스트 로드"
  "/sc:spawn": "작업 오케스트레이션"
```

### 🎭 11개 페르소나 시스템
```yaml
기술_전문가:
  "--persona-architect": "시스템 설계, 확장성"
  "--persona-frontend": "UI/UX, 접근성"
  "--persona-backend": "API, 신뢰성"
  "--persona-security": "보안, 취약점"
  "--persona-performance": "최적화, 병목제거"

프로세스_전문가:
  "--persona-analyzer": "근본원인 분석"
  "--persona-qa": "품질보증, 테스팅"
  "--persona-refactorer": "코드품질, 기술부채"
  "--persona-devops": "인프라, 자동화"

지식_전문가:
  "--persona-mentor": "교육, 지식전달"
  "--persona-scribe=ko": "문서작성, 한국어"
```

### 🌊 Wave System (자동 활성화)
```yaml
wave_activation:
  조건: "complexity ≥0.7 + files >20 + types >2"
  지원명령어: [analyze, build, design, implement, improve, task]
  전략:
    progressive: "점진적 개선"
    systematic: "체계적 분석"
    adaptive: "동적 구성"
```

### 🔧 MCP 서버 (4개 작동 확인)
```yaml
작동_서버:
  Context7: "라이브러리 문서, 패턴"
  Sequential: "복잡한 다단계 분석"
  Magic: "UI 컴포넌트 생성"
  Playwright: "브라우저 자동화, E2E"

사용법:
  "--c7": "Context7 활성화"
  "--seq": "Sequential 활성화"
  "--magic": "Magic UI 생성"
  "--play": "Playwright 테스팅"
```

## 💻 프로젝트 현황

### 📊 현재 상태
tech_stack:
  backend: "Rust + Tauri v2.0"
  frontend: "React 18.2 + TypeScript 5.0"
  ai: "Google Gemini API"
  database: "SQLite + rusqlite 0.29"
```

### 🎯 핵심 파일
```yaml
번역엔진: "src-tauri/src/translation_executor.rs"
프롬프트: "src-tauri/src/prompts/structured_prompts.rs"
UI메인: "src/App.tsx"
용어집: "src-tauri/src/glossary_manager.rs"
```

### ⚡ 검증된 최적화
```yaml
wave_methodology:
  discovery: "체계적 병목 조사"
  wave_1: "90% 병목 제거 (Arc<Mutex> → Arc)"
  wave_2: "8% 성능 향상 (Rate Limiter 제거)"
  wave_3: "2% 미세 튜닝 (HTTP/2, 캐시)"
```

## 🛠️ 실전 활용 예시

### SuperClaude 명령어 조합
```bash
# 1. 성능 문제 분석
/sc:analyze --focus performance --persona-performance --seq
/sc:troubleshoot slow-translation --think-hard

# 2. 새 기능 구현
/sc:implement batch-progress-ui --type component --framework react --magic
/sc:design translation-memory --persona-architect

# 3. 코드 품질 개선
/sc:improve src-tauri/src/*.rs --quality --loop
/sc:cleanup --persona-refactorer

# 4. 보안 감사
/sc:analyze --focus security --persona-security --ultrathink
```

### 페르소나 조합 패턴
```yaml
성능최적화: "--persona-performance --persona-architect"
UI개발: "--persona-frontend --persona-qa"
API설계: "--persona-backend --persona-security"
문서작성: "--persona-scribe=ko --persona-mentor"
```

## 📚 문서 참조

### 프로젝트 문서
```yaml
세부학습: "CLAUDE_NOTES.md"

### SuperClaude 문서
```yaml
사용자가이드: "G:/SuperClaude/Docs/superclaude-user-guide.md"
명령어가이드: "G:/SuperClaude/Docs/commands-guide.md"
페르소나가이드: "G:/SuperClaude/Docs/personas-guide.md"
```

## 🚫 중요 안티패턴

### 절대 금지사항
```yaml
디버깅_로그:
  rule: "println!, console.log 절대 금지"
  reason: "실제 문제 해결에 도움 안됨"
  alternative: "코드 직접 분석"

전역_락:
  problem: "TRANSLATION_CACHE.lock() 병목"
  solution: "캐시 비활성화"

품질_검증:
  problem: "validate_quality 병렬 차단"
  solution: "validate_quality: false"
```

## 🎯 즉시 처리 규칙

### 자동 처리
```yaml
병렬화: "독립 작업 자동 병렬 실행"
압축: "컨텍스트 75% 초과시 자동"
페르소나: "도메인 키워드로 자동 활성화"
Wave: "복잡도 0.7 이상시 자동"
```

### SuperClaude 품질 게이트 (공식)
```yaml
8단계_검증:
  1_구문: "언어 파서 검증"
  2_타입: "타입 호환성"
  3_린트: "품질 규칙"
  4_보안: "취약점 평가"
  5_테스트: "커버리지 80%+"
  6_성능: "벤치마크"
  7_문서: "완성도"
  8_통합: "호환성"
```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
## 🎯 마지막 리마인더 (Lost in the Middle 해결)
```yaml
핵심_원칙:
  - "한국어로 대화"
  - "디버깅 로그 금지"
  - "SuperClaude 명령어 활용"
  - "페르소나 시스템 적극 사용"
  - "병렬 처리 우선"
```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

*📝 버전: v4.0 (SuperClaude 통합)*
*🚀 상태: 16개 명령어 + 11개 페르소나 활성화*
*⭐ 성과: 20-30배 성능 + AI 고급 기능*
*📅 업데이트: 2025-01-08*
*⚠️ 주의: 이 파일의 핵심 구조는 신성불가침 - 절대 변경 금지*