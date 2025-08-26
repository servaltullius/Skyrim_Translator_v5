# X-TR Gemini Translator (Alduin)

베데스다 게임용 xTranslator XML 파일을 Google Gemini 1.5 Flash를 이용해 실시간으로 번역하는 전문 데스크톱 도구입니다.

## 🎯 주요 기능

- **실시간 번역**: 항목별 실시간 번역 진행 상황 표시
- **태그 보존**: HTML 태그, 변수, 플레이스홀더 100% 보존
- **용어집 관리**: JSON 기반 번역 일관성 유지
- **가상 스크롤**: 수만 개 항목도 부드럽게 처리
- **고성능**: 동시 처리로 빠른 번역 속도

## 🛠️ 기술 스택

- **Frontend**: React + TypeScript + Vite + TailwindCSS
- **Backend**: Tauri + Rust
- **AI**: Google Gemini 1.5 Flash API
- **UI**: Radix UI + shadcn/ui 컴포넌트
- **데이터**: SQLite + 실시간 이벤트

## 📋 사전 요구사항

- Windows 11
- WebView2 Runtime (Windows 11에 기본 포함)
- Google Gemini API 키

## 🚀 개발 환경 설정

### 1. 종속성 설치

```bash
# 프로젝트 디렉토리로 이동
cd xtr-gemini-translator

# Node.js 종속성 설치
npm install

# Rust 종속성 (자동으로 설치됨)
```

### 2. 개발 서버 실행

```bash
npm run dev
```

### 3. 빌드

```bash
npm run build
npm run tauri build
```

## 📁 프로젝트 구조

```
xtr-gemini-translator/
├── src/                    # React 프론트엔드
│   ├── components/         # UI 컴포넌트
│   ├── api/               # Tauri API 연동
│   └── state/             # 상태 관리
├── src-tauri/             # Rust 백엔드
│   ├── src/
│   │   ├── llm/          # Gemini API 연동
│   │   ├── textops/      # 텍스트 처리 (마스킹/복원)
│   │   └── commands.rs   # Tauri 명령어
│   └── Cargo.toml
└── docs/                  # 설계 문서
```

## 🎮 지원되는 게임

- Skyrim (The Elder Scrolls V)
- Fallout 4
- 기타 xTranslator 지원 베데스다 게임

## 📄 라이선스

MIT License

## 📞 문의

프로젝트 관련 문의사항이나 버그 리포트는 GitHub Issues를 이용해 주세요.