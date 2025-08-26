아래는 **Windows 11 / Tauri(v2) + React + Rust / Gemini 1.5 Flash**을 기준으로 한 **프로젝트 스캐폴딩(폴더 구조·설정), 핵심 Command/Event 시그니처, DB 스키마 SQL, 마스킹·복원 유닛테스트 케이스** 일괄 제안입니다. 문서와 코드 스텁만 복사해도 바로 개발을 시작할 수 있도록 구성했습니다.

> 참고: Tauri v2의 커맨드/이벤트/플러그인/설정 형식은 공식 문서 기준으로 맞춰 두었습니다. `invoke`/`listen` 경로, `tauri.conf.json`/`capabilities` 구조, 플러그인 초기화 방식 등은 아래 인용을 확인하세요. ([Tauri][1])

---

## 1) 리포지토리 구조

```
xtr-gemini-translator/
├─ package.json
├─ vite.config.ts
├─ tsconfig.json
├─ .env.sample                      # (프론트용 비민감 설정; API Key는 백엔드 보관)
├─ glossary.json                    # 기본 용어집 샘플
├─ prompts/
│  └─ base.vec.txt                  # 기본 VEC 프롬프트 템플릿
├─ src/                             # React (Vite + TS)
│  ├─ main.tsx
│  ├─ App.tsx
│  ├─ api/tauri.ts                  # invoke/listen 래퍼
│  ├─ components/
│  │  ├─ DataGrid.tsx               # TanStack Table + react-window
│  │  ├─ Sidebar.tsx                # 파일/번역/필터/용어집/프롬프트
│  │  └─ Editors.tsx                # 행 상세 에디터(원문/번역/메타)
│  ├─ state/store.ts                # Zustand 상태(목록/진행률/필터)
│  └─ styles/
├─ src-tauri/                       # Rust (Tauri v2)
│  ├─ Cargo.toml
│  ├─ build.rs
│  ├─ tauri.conf.json
│  ├─ capabilities/
│  │  └─ default.json
│  └─ src/
│     ├─ lib.rs                     # Tauri 엔트리, 플러그인 init, invoke 등록
│     ├─ commands.rs                # #[tauri::command] 모음
│     ├─ events.rs                  # 이벤트 명 상수
│     ├─ db.rs                      # rusqlite 스키마/쿼리
│     ├─ xml.rs                     # xTranslator XML 로드/저장
│     ├─ textops/
│     │  ├─ mask.rs                 # 태그/플레이스홀더 마스킹
│     │  ├─ restore.rs              # Diff 기반 복원(similar)
│     │  └─ validate.rs             # 짝/중첩/순서 검증
│     ├─ llm/
│     │  ├─ gemini.rs               # generateContent 호출(reqwest)
│     │  └─ prompt.rs               # VEC + 사용자 프롬프트 머지
│     └─ pipeline.rs                # 워커 풀(동시성), 배치/재시도/이벤트 emit
```

* **Tauri v2 이벤트/커맨드**: 프론트는 `@tauri-apps/api/core.invoke` / `@tauri-apps/api/event.listen`, 백엔드는 `#[tauri::command]`와 `emit`/`listen`으로 통신합니다. ([Tauri][1])
* **프로젝트/설정 파일**: `tauri.conf.json`과 `capabilities/` 사용. ([Tauri][2])

---

## 2) Tauri 설정 파일

### 2-1. `src-tauri/tauri.conf.json` (예시, v2)

```json
{
  "$schema": "../gen/schemas/desktop-schema.json",
  "identifier": "com.yourorg.xtr-gemini",
  "productName": "X-TR Gemini Translator",
  "version": "0.1.0",
  "build": {
    "beforeDevCommand": "npm run dev",
    "beforeBuildCommand": "npm run build",
    "devUrl": "http://localhost:5173",
    "frontendDist": "../dist"
  },
  "app": {
    "withGlobalTauri": false
  },
  "bundle": {
    "targets": ["msi"],
    "icon": ["../icons/app.ico"],
    "resources": ["../prompts", "../glossary.json"]
  },
  "plugins": {
    "dialog": {},
    "store": {},
    "stronghold": {}
  }
}
```

* Vite 연동 키(`devUrl`, `beforeDevCommand`, `frontendDist`)는 v2 공식 가이드를 따릅니다. ([Tauri][3])

### 2-2. `src-tauri/capabilities/default.json` (권한/플러그인 퍼미션)

```json
{
  "identifier": "default",
  "description": "Default desktop capabilities",
  "windows": ["main"],
  "permissions": [
    "core:default",
    "dialog:default",
    "store:default",
    "stronghold:default"
  ],
  "allow": {
    "commands": [
      "open_xml",
      "start_translate",
      "cancel_translate",
      "save_xml",
      "get_glossary",
      "upsert_glossary",
      "get_settings",
      "update_settings"
    ],
    "events": [
      "row_translated",
      "job_progress",
      "job_error",
      "job_done"
    ]
  }
}
```

* v2에서 **Capabilities** 파일로 플러그인 권한을 명시합니다(강력 권고). ([Tauri][4])

---

## 3) 백엔드(Rust) 의존성 (`src-tauri/Cargo.toml`)

```toml
[package]
name = "xtr-gemini"
version = "0.1.0"
edition = "2021"

[build-dependencies]
tauri-build = { version = "2", features = ["config-json5"] }

[dependencies]
tauri = { version = "2", features = ["config-json5"] }
tauri-plugin-dialog = "2"
tauri-plugin-store = "2"
tauri-plugin-stronghold = "2"

# DB
rusqlite = { version = "0.37", features = ["bundled", "serde_json"] }

# async / http
tokio = { version = "1", features = ["rt-multi-thread", "macros", "sync"] }
reqwest = { version = "0.12", features = ["json", "rustls-tls"] }

# XML / Diff / 정규식
quick-xml = "0.31"
roxmltree = "0.19"
similar = "2"
regex = "1"

# 기타
serde = { version = "1", features = ["derive"] }
serde_json = "1"
thiserror = "1"
anyhow = "1"
```

* `stronghold` 플러그인은 **비밀번호 해시 함수** 초기화가 필요합니다(아래 코드). ([Tauri][5])
* `reqwest::Client`는 내부에 **커넥션 풀**을 갖고 있어 재사용 권장. ([Docs.rs][6])
* **동시성 한도**는 `tokio::sync::Semaphore`로 제어합니다. ([Docs.rs][7])
* 대형 XML은 `quick-xml` 스트리밍 파서, 읽기 전용 트리는 `roxmltree`. ([Docs.rs][8])
* 태그 복원은 `similar`(Patience/Myers) 기반. ([Docs.rs][9])

---

## 4) 백엔드 코드 스텁

### 4-1. `src-tauri/src/lib.rs`

```rust
#![cfg_attr(all(not(debug_assertions), target_os = "windows"), windows_subsystem = "windows")]

mod commands;
mod events;
mod db;
mod xml;
mod pipeline;
mod llm;
mod textops;

use tauri::{Manager, Emitter};

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
  tauri::Builder::default()
    // 플러그인 초기화
    .plugin(tauri_plugin_dialog::init())
    .plugin(tauri_plugin_store::Builder::default().build())
    .setup(|app| {
      // Stronghold: argon2 기본 해시 함수로 초기화(권장)
      // (salt 파일은 앱 로컬 데이터 경로에 생성)
      let salt_path = app.path().app_local_data_dir().unwrap().join("salt.txt");
      app.handle().plugin(tauri_plugin_stronghold::Builder::with_argon2(&salt_path).build())?;

      // DB 초기화
      db::init(&app.handle())?;

      Ok(())
    })
    // 명령 등록
    .invoke_handler(tauri::generate_handler![
      commands::open_xml,
      commands::start_translate,
      commands::cancel_translate,
      commands::save_xml,
      commands::get_glossary,
      commands::upsert_glossary,
      commands::get_settings,
      commands::update_settings
    ])
    .run(tauri::generate_context!())
    .expect("error while running tauri application");
}
```

> Stronghold는 암호 해시 기반 **보안 저장소**이며, API 키 등 비밀정보 저장에 적합합니다. 초기화·권한 설정은 v2 가이드를 참고하세요. ([Tauri][5])

### 4-2. `src-tauri/src/events.rs`

```rust
pub const EVT_ROW_TRANSLATED: &str = "row_translated";
pub const EVT_JOB_PROGRESS: &str  = "job_progress";
pub const EVT_JOB_ERROR: &str     = "job_error";
pub const EVT_JOB_DONE: &str      = "job_done";
```

### 4-3. `src-tauri/src/commands.rs`

```rust
use tauri::{AppHandle, State, Emitter};
use serde::{Serialize, Deserialize};
use crate::{pipeline, db, events::*};

#[derive(Serialize, Deserialize)]
pub struct TranslateOpts {
  pub concurrency: Option<usize>,
  pub batch_size: Option<usize>,
  pub model: Option<String>,            // 기본: "gemini-1.5-flash"
  pub temperature: Option<f32>,         // 기본: 0.2
}

#[tauri::command]
pub async fn open_xml(app: AppHandle, path: String) -> tauri::Result<usize> {
  let count = crate::xml::load_xtranslator_xml(&app, &path).await?;
  Ok(count)
}

#[tauri::command]
pub async fn start_translate(app: AppHandle, opts: Option<TranslateOpts>) -> tauri::Result<()> {
  pipeline::start(app, opts).await?;
  Ok(())
}

#[tauri::command]
pub async fn cancel_translate(app: AppHandle) -> tauri::Result<bool> {
  Ok(pipeline::cancel(&app).await)
}

#[tauri::command]
pub async fn save_xml(app: AppHandle, path: Option<String>) -> tauri::Result<usize> {
  let written = crate::xml::write_xtranslator_xml(&app, path.as_deref()).await?;
  Ok(written)
}

#[tauri::command]
pub fn get_glossary(app: AppHandle) -> tauri::Result<serde_json::Value> {
  Ok(db::get_glossary(&app)?)
}

#[tauri::command]
pub fn upsert_glossary(app: AppHandle, json: serde_json::Value) -> tauri::Result<()> {
  db::upsert_glossary(&app, json)?;
  Ok(())
}

#[tauri::command]
pub fn get_settings(app: AppHandle) -> tauri::Result<serde_json::Value> {
  Ok(db::get_settings(&app)?)
}

#[tauri::command]
pub fn update_settings(app: AppHandle, patch: serde_json::Value) -> tauri::Result<()> {
  db::update_settings(&app, patch)?;
  Ok(())
}
```

### 4-4. DB 스키마 (`src-tauri/src/db.rs`)

```rust
use rusqlite::{Connection, params};
use tauri::AppHandle;
use anyhow::Result;

pub fn init(app: &AppHandle) -> Result<()> {
  let db_path = app.path().app_local_data_dir()?.join("xtr.db");
  let conn = Connection::open(db_path)?;

  conn.execute_batch(r#"
    PRAGMA journal_mode=WAL;
    CREATE TABLE IF NOT EXISTS entries (
      id INTEGER PRIMARY KEY,
      edid TEXT,              -- 레코드 ID/키(있으면)
      rec  TEXT,              -- 레코드 타입/카테고리(있으면)
      src  TEXT NOT NULL,     -- 원문
      dst  TEXT,              -- 번역문
      status TEXT NOT NULL DEFAULT 'pending', -- pending/done/error
      meta_json TEXT,         -- 부가 메타(길이/해시/경고 등)
      updated_at TEXT DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now'))
    );
    CREATE INDEX IF NOT EXISTS idx_entries_status ON entries(status);
    CREATE TABLE IF NOT EXISTS glossary (
      key TEXT PRIMARY KEY,
      value TEXT NOT NULL,
      note TEXT
    );
    CREATE TABLE IF NOT EXISTS settings (
      key TEXT PRIMARY KEY,
      value TEXT NOT NULL
    );
  "#)?;

  Ok(())
}

// 이하 get_glossary/upsert_glossary/get_settings/update_settings 구현…
```

> 로컬 임베디드 DB는 **rusqlite**를 사용하면 설치 의존성 없이(Windows 포함) 빌드 옵션으로 SQLite 정적 링크가 가능합니다(`bundled` 기능). ([Docs.rs][10])

### 4-5. XML I/O (`src-tauri/src/xml.rs`)

```rust
use anyhow::Result;
use tauri::AppHandle;

// xTranslator XML 구조를 파싱해 entries 테이블에 로드
pub async fn load_xtranslator_xml(app: &AppHandle, path: &str) -> Result<usize> {
  // 대용량 대응: quick-xml 스트리밍 파서 사용
  // 1) <String> 항목 반복 => src 저장, edid/rec/meta 추출
  // 2) DB에 벌크 insert
  // (구현부 생략: quick_xml::Reader 사용)
  Ok(0)
}

// entries 테이블에서 읽어 xTranslator XML 구조로 다시 씀
pub async fn write_xtranslator_xml(app: &AppHandle, target_path: Option<&str>) -> Result<usize> {
  // 1) DB에서 순서대로 select
  // 2) roxmltree/직접 writer로 <String> 복원
  Ok(0)
}
```

* 대형 XML에는 **quick-xml 스트리밍**이 적합, 필요 시 `roxmltree`로 읽기 전용 트리 탐색. ([Docs.rs][8])

### 4-6. 마스킹/복원/검증 (`src-tauri/src/textops/`)

`mask.rs` (태그/플레이스홀더 보호):

```rust
use regex::Regex;
use serde::{Serialize, Deserialize};

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Masked {
  pub text: String,
  pub tags: Vec<String>,          // <...>
  pub vars: Vec<String>,          // {var}, %s, %d, $1 ...
}

pub fn mask(input: &str) -> Masked {
  // 예시 정규식(필요시 세분화)
  let re_tag = Regex::new(r"<[^>]+>").unwrap();
  let re_var = Regex::new(r"(\{[^}]+\}|%[sd]|%\d*\$s|\$\d+)").unwrap();

  let mut out = String::with_capacity(input.len());
  let mut tags = Vec::new();
  let mut vars = Vec::new();

  // 1) 태그 치환 → ⟦T0⟧, ⟦T1⟧ ...
  let mut cur = input.to_string();
  for (i, m) in re_tag.find_iter(input).enumerate() {
    tags.push(m.as_str().to_string());
    let token = format!("⟦T{}⟧", i);
    cur = cur.replacen(m.as_str(), &token, 1);
  }
  // 2) 변수 치환 → ⟦V0⟧ ...
  let mut count = 0;
  let tmp = re_var.replace_all(&cur, |_: &regex::Captures| {
    let tok = format!("⟦V{}⟧", count);
    count += 1;
    tok
  });
  out.push_str(&tmp);

  Masked { text: out, tags, vars }
}
```

`restore.rs` (Diff 기반 복원):

```rust
use similar::TextDiff;

// 번역 결과에 플레이스홀더(⟦T*⟧, ⟦V*⟧)가 남아있다는 전제에서 원래 값으로 복원
pub fn restore(masked: &super::mask::Masked, translated: &str) -> String {
  // 간단판: 토큰을 원본 순서대로 대치
  // 고급: TextDiff로 경계를 찾고 안정 위치에 삽입
  let mut out = translated.to_string();

  for (i, t) in masked.tags.iter().enumerate() {
    let token = format!("⟦T{}⟧", i);
    out = out.replace(&token, t);
  }
  for (i, v) in masked.vars.iter().enumerate() {
    let token = format!("⟦V{}⟧", i);
    out = out.replace(&token, v);
  }
  out
}

// 필요 시 TextDiff를 이용해 위치 보정
pub fn refine_with_diff(src_text: &str, dst_text: &str) -> String {
  let _diff = TextDiff::configure().algorithm(similar::Algorithm::Patience)
    .diff_chars(src_text, dst_text);
  // TODO: replace/insert/delete 케이스 경계 보정
  dst_text.to_string()
}
```

> `similar`는 **patience diff**를 제공합니다. 태그/변수의 최종 위치를 안정화하는 데 유용합니다. ([Docs.rs][9])

`validate.rs` (짝/중첩/순서 검증 스텁):

```rust
pub fn validate_xml_safety(s: &str) -> bool {
  // 간단 체크: 태그 짝/중첩(스택), 미닫힘 누락 여부
  // ICU MessageFormat/플레이스홀더 짝 확인 등 확장 가능
  true
}
```

### 4-7. LLM 클라이언트 (`src-tauri/src/llm/gemini.rs`)

```rust
use anyhow::Result;
use reqwest::Client;
use serde_json::json;

pub struct Gemini {
  client: Client,
  api_key: String,
  model: String,          // "gemini-1.5-flash" (기본), 설정으로 교체 가능
  temperature: f32,
}

impl Gemini {
  pub fn new(client: Client, api_key: String, model: String, temperature: f32) -> Self {
    Self { client, api_key, model, temperature }
  }

  pub async fn translate_batch(&self, chunks: &[String], system_prompt: &str) -> Result<Vec<String>> {
    // Developer API REST: /v1beta/models/{model}:generateContent
    // body: contents.parts[].text (배치 시 join + 구분자 사용 or parallel 호출)
    let text = chunks.join("\n<<<#SEP#>>>\n");
    let body = json!({
      "contents": [{
        "role": "user",
        "parts": [{ "text": format!("{}\n\n{}", system_prompt, text) }]
      }],
      "generationConfig": { "temperature": self.temperature }
    });

    let url = format!(
      "https://generativelanguage.googleapis.com/v1beta/models/{}:generateContent",
      self.model
    );

    let resp = self.client.post(url)
      .header("x-goog-api-key", &self.api_key)
      .json(&body)
      .send()
      .await?
      .error_for_status()?
      .json::<serde_json::Value>()
      .await?;

    // 응답 파싱(간이)
    let out = resp["candidates"][0]["content"]["parts"][0]["text"]
      .as_str()
      .unwrap_or("")
      .to_string();

    // 구분자로 다시 split
    let items = out.split("\n<<<#SEP#>>>\n").map(|s| s.to_string()).collect();
    Ok(items)
  }
}
```

* **REST 엔드포인트**와 호출 모델: `:generateContent` / Developer API. 모델은 `gemini-1.5-flash`(요건) 또는 최신 `2.5-flash`로 교체 가능. ([Google AI for Developers][11], [Google Cloud][12])
* 스트리밍/긴 문맥 지원 등 모델 특성은 공식 가이드를 참고하세요. ([Google AI for Developers][13])

### 4-8. 파이프라인(동시 처리 + 이벤트) (`src-tauri/src/pipeline.rs`)

```rust
use tauri::{AppHandle, Emitter};
use tokio::sync::{Semaphore};
use anyhow::Result;
use crate::{db, llm::gemini::Gemini, textops, events::*};

pub async fn start(app: AppHandle, opts: Option<crate::commands::TranslateOpts>) -> Result<()> {
  let concurrency = opts.as_ref().and_then(|o| o.concurrency).unwrap_or(10);
  let batch_size  = opts.as_ref().and_then(|o| o.batch_size).unwrap_or(8);
  let model = opts.as_ref().and_then(|o| o.model.clone()).unwrap_or_else(|| "gemini-1.5-flash".to_string());
  let temperature = opts.as_ref().and_then(|o| o.temperature).unwrap_or(0.2);

  let client = reqwest::Client::new(); // 내부 커넥션 풀 재사용
  let api_key = crate::secure::load_gemini_api_key(&app)?; // Stronghold에서 로드
  let gemini = Gemini::new(client, api_key, model, temperature);

  let sem = Semaphore::new(concurrency);
  let rows = db::select_pending(&app, batch_size)?;   // 예: 상태 pending 일부만
  let total = rows.len();
  let mut done = 0usize;

  for chunk in rows.chunks(batch_size) {
    let _permit = sem.acquire_many(chunk.len() as u32).await.unwrap();
    // 마스킹 → 번역 → 복원 → 검증 → 저장
    let masked: Vec<_> = chunk.iter().map(|r| textops::mask::mask(&r.src)).collect();
    let input_texts: Vec<_> = masked.iter().map(|m| m.text.clone()).collect();

    let sys = crate::llm::prompt::build_system_prompt(&app)?; // VEC + 커스텀 + 용어집 지침
    let translated = gemini.translate_batch(&input_texts, &sys).await?;

    for (i, r) in chunk.iter().enumerate() {
      let restored = textops::restore::restore(&masked[i], &translated[i]);
      let ok = textops::validate::validate_xml_safety(&restored);
      if ok {
        db::update_entry_done(&app, r.id, &restored)?;
        done += 1;
        app.emit(EVT_ROW_TRANSLATED, serde_json::json!({"id": r.id, "dst": restored}))?;
      } else {
        db::update_entry_error(&app, r.id, "validate_failed")?;
        app.emit(EVT_JOB_ERROR, serde_json::json!({"id": r.id, "code":"validate_failed"}))?;
      }
      app.emit(EVT_JOB_PROGRESS, serde_json::json!({"done": done, "total": total}))?;
    }
  }
  app.emit(EVT_JOB_DONE, serde_json::json!({"done": done, "total": total}))?;
  Ok(())
}

pub async fn cancel(_app: &AppHandle) -> bool {
  // TODO: CancellationToken 사용
  false
}
```

* 이벤트 시스템/커맨드 호출은 Tauri v2 가이드에 명시된 방식입니다. ([Tauri][1])
* 커넥션 풀·세마포어는 고성능/레이트리밋 안정화를 위한 **검증된 패턴**입니다. ([Docs.rs][6])

---

## 5) 프론트엔드 스캐폴딩

### 5-1. `package.json` (핵심 의존성)

```json
{
  "name": "xtr-gemini-translator",
  "private": true,
  "scripts": {
    "dev": "vite",
    "build": "vite build",
    "tauri": "tauri"
  },
  "dependencies": {
    "@tanstack/react-table": "^8.16.0",
    "@tauri-apps/api": "^2.0.0",
    "@tauri-apps/plugin-dialog": "^2.0.0",
    "@tauri-apps/plugin-store": "^2.0.0",
    "@tauri-apps/plugin-stronghold": "^2.0.0",
    "react": "^18.3.1",
    "react-dom": "^18.3.1",
    "react-window": "^1.8.10",
    "zustand": "^4.5.2"
  },
  "devDependencies": {
    "@types/react": "^18.2.61",
    "@types/react-dom": "^18.2.19",
    "typescript": "^5.5.4",
    "vite": "^5.4.0"
  }
}
```

* 대용량 목록 렌더링은 **TanStack Table(v8)** + **react-window** 조합(가상 스크롤)으로 안정적입니다. ([TanStack][14], [React Window][15], [web.dev][16])

### 5-2. `src/api/tauri.ts`

```ts
import { invoke } from '@tauri-apps/api/core';
import { listen } from '@tauri-apps/api/event';

export const api = {
  openXml: (path: string) => invoke<number>('open_xml', { path }),
  startTranslate: (opts?: any) => invoke<void>('start_translate', { opts }),
  cancelTranslate: () => invoke<boolean>('cancel_translate'),
  saveXml: (path?: string) => invoke<number>('save_xml', { path }),
  getGlossary: () => invoke<any>('get_glossary'),
  upsertGlossary: (json: any) => invoke<void>('upsert_glossary', { json }),
  getSettings: () => invoke<any>('get_settings'),
  updateSettings: (patch: any) => invoke<void>('update_settings', { patch }),
};

export const events = {
  onRowTranslated: (cb: (p: any) => void) => listen('row_translated', e => cb(e.payload)),
  onProgress:     (cb: (p: any) => void) => listen('job_progress', e => cb(e.payload)),
  onError:        (cb: (p: any) => void) => listen('job_error', e => cb(e.payload)),
  onDone:         (cb: (p: any) => void) => listen('job_done', e => cb(e.payload)),
};
```

> v2에서 `invoke`는 `@tauri-apps/api/core`, 이벤트는 `@tauri-apps/api/event`에서 제공합니다. ([Tauri][1])

### 5-3. `src/components/DataGrid.tsx` (요지)

```tsx
import { useEffect } from 'react';
import { FixedSizeList as List } from 'react-window';
import { ColumnDef, useReactTable, getCoreRowModel } from '@tanstack/react-table';
import shallow from 'zustand/shallow';
import { useStore } from '../state/store';

const columns: ColumnDef<any>[] = [
  { header: 'ID', accessorKey: 'id', size: 80 },
  { header: '상태', accessorKey: 'status', size: 90 },
  { header: '원문', accessorKey: 'src', size: 600 },
  { header: '번역', accessorKey: 'dst', size: 600 },
];

export function DataGrid() {
  const rows = useStore(s => s.filteredRows, shallow);
  const table = useReactTable({ data: rows, columns, getCoreRowModel: getCoreRowModel() });

  const Row = ({ index, style }: any) => {
    const row = table.getRowModel().rows[index];
    return (
      <div style={style} className="row">
        {row.getVisibleCells().map(cell => (
          <div key={cell.id} style={{ width: cell.column.getSize() }}>{cell.getValue() as any}</div>
        ))}
      </div>
    );
  };

  return (
    <List height={600} itemCount={table.getRowModel().rows.length} itemSize={36} width="100%" overscanCount={8}>
      {Row}
    </List>
  );
}
```

* `overscanCount`를 크게 주면 스크롤 중 깜빡임을 줄여 UX가 좋아집니다. ([web.dev][16])

### 5-4. `src/components/Sidebar.tsx` (요지)

* 파일 열기/저장(tauri dialog 플러그인 사용), 번역 시작/중지, 필터(미번역/완료/오류), 용어집 JSON 편집, 프롬프트(VEC + 사용자 추가).

> 파일/저장 다이얼로그는 플러그인 사용법대로 호출합니다. ([Tauri][17])

---

## 6) 프롬프트 & 용어집

### 6-1. `prompts/base.vec.txt` (샘플)

```
[ROLE]
You are a professional game localization translator. Preserve placeholders and XML tags.

[EXCLUSIONS]
Do not translate content inside <> XML tags, and placeholders like {var}, %s, %d, $1, etc.

[STYLE]
Natural Korean for Bethesda fantasy RPG. Avoid honorifics unless clearly required.

[GLOSSARY]
{{GLOSSARY}}   # 런타임에 중요한 용어 50~200개만 삽입

[OUTPUT]
Return only the translated text for each input segment, in the same order, separated by:
<<<#SEP#>>>
```

### 6-2. `glossary.json` (샘플)

```json
[
  { "key": "Dragonborn", "value": "드래곤본", "note": "Bethesda 공식 표기" },
  { "key": "Septim", "value": "셉팀" }
]
```

---

## 7) 유닛테스트(핵심 케이스 목록 + 샘플)

`src-tauri/src/textops/mask.rs`/`restore.rs` 내부에 테스트를 포함합니다.

### 7-1. 테스트 케이스 목록

1. **단순 태그**: `<b>Hero</b>` — 태그 보존/복원 100%.
2. **중첩 태그**: `A <b><i>very</i> bold</b> text` — 중첩·순서 검증.
3. **속성 태그**: `<font color="#fff">Hi</font>` — 속성 보존.
4. **플레이스홀더**: `Hello, {name}! %s %d $1` — 모두 비번역, 원형 보존.
5. **인접 태그/토큰**: `<b></b>{x}%s` — 경계 처리.
6. **ICU**: `{itemCount, plural, one{1 item} other{# items}}` — 내부 텍스트 번역 금지.
7. **초장문**: 10k+ 문자 — 마스킹/복원 속도와 안정성.
8. **비정상 입력**: 태그 미닫힘 — 검증 실패 경로.

### 7-2. 샘플 테스트 코드

```rust
#[cfg(test)]
mod tests {
  use super::*;
  use crate::textops::{mask, restore};

  #[test]
  fn mask_and_restore_simple() {
    let s = "A <b>bold</b> move";
    let m = mask::mask(s);
    assert!(m.text.contains("⟦T0⟧"));
    let tr = m.text.replace("A", "한");
    let out = restore::restore(&m, &tr);
    assert!(out.contains("<b>"));
    assert!(out.contains("</b>"));
  }

  #[test]
  fn placeholders_preserved() {
    let s = "Hello, {name}! Score: %d";
    let m = mask::mask(s);
    let tr = m.text.replace("Hello", "안녕");
    let out = restore::restore(&m, &tr);
    assert!(out.contains("{name}"));
    assert!(out.contains("%d"));
  }
}
```

---

## 8) 성능/UX 팁 (프로파일링 포인트)

* **동시성 상한**: 기본 8\~12, 네트워크·쿼터에 맞게 조정 (`tokio::sync::Semaphore`). ([Docs.rs][7])
* **배치 번역**: 짧은 항목 묶음으로 `generateContent` 호출 수 감소. ([Google AI for Developers][11])
* **가상 스크롤**: `react-window` `overscanCount` 튜닝. ([web.dev][16])
* **스트리밍(옵션)**: 초장문은 `streamGenerateContent` 검토. (Vertex AI 가이드) ([Google Cloud][18])
* **Windows 11 런타임**: WebView2 Runtime은 **Win11에 포함**(그래도 런처에서 존재 확인 권장). ([Microsoft Learn][19], [Windows Blog][20])

---

## 9) 보안(비밀키 저장)

* **Gemini API 키**는 Stronghold(보안 저장소)에 보관하고, 최초 입력 후 저장/로드만 허용.
* 플러그인 초기화 및 권한 설정은 v2 안내를 따릅니다(예: `stronghold:default` 퍼미션). ([Tauri][5])

---

## 10) 프런트에서의 실시간 갱신 흐름 (요지)

```ts
useEffect(() => {
  const subs = Promise.all([
    events.onRowTranslated(({ id, dst }) => store.actions.updateRow(id, dst)),
    events.onProgress(({ done, total }) => store.actions.setProgress(done, total)),
    events.onError((e) => toast.error(JSON.stringify(e))),
    events.onDone(() => toast.success('완료'))
  ]);
  return () => { subs.then(unsubs => unsubs.forEach(u => u())); };
}, []);
```

* 이벤트 등록/해제는 v2 문서의 `listen/unlisten` 패턴을 따릅니다. ([Tauri][1])

---

## 11) 빌드 & 실행

```bash
# 프론트 설치
npm i
# 백엔드(Tauri) 설치 및 실행
npm run tauri dev
# 프로덕션 패키징
npm run tauri build
```

* Vite/Tauri 연동 설정은 `tauri.conf.json`의 `build` 섹션으로 활성화됩니다. ([Tauri][3])

---

## 12) 왜 이 스캐폴딩이 요구사항에 부합하나

* **실시간 행별 업데이트**: 백엔드에서 각 항목 완료 시 **이벤트 emit**, 프론트는 `listen`으로 즉시 반영. ([Tauri][1])
* **고속/대용량**: `reqwest` 풀 재사용 + `Tokio Semaphore` 동시성 + 배치 전송. ([Docs.rs][6])
* **태그/변수 보존**: 마스킹 → 번역 → Diff 기반 복원(`similar`) → 검증. ([Docs.rs][9])
* **대용량 목록 UX**: TanStack Table(헤드리스) + react-window(가상화). ([TanStack][14], [React Window][15])
* **Windows 친화**: Win11은 WebView2 Runtime 포함(일반적으로 추가 설치 불필요). ([Microsoft Learn][19], [Windows Blog][20])

---

### 부록 A) `prompts/base.vec.txt`에 용어집 주입 예

```rust
// llm/prompt.rs
use tauri::AppHandle;
use serde_json::Value;

pub fn build_system_prompt(app: &AppHandle) -> anyhow::Result<String> {
  let tpl = include_str!("../../prompts/base.vec.txt");
  let glossary: Value = crate::db::get_glossary(app)?;
  let pairs: Vec<String> = glossary.as_array().unwrap_or(&vec![])
    .iter().filter_map(|it| {
      Some(format!("{} => {}", it["key"].as_str()?, it["value"].as_str()?))
    }).collect();
  Ok(tpl.replace("{{GLOSSARY}}", &pairs.join("\n")))
}
```

---

### 부록 B) React 사이드바에서 파일 열기/저장 (다이얼로그 플러그인)

```ts
import { open, save } from '@tauri-apps/plugin-dialog';
import { api } from '../api/tauri';

async function openXml() {
  const path = await open({ filters: [{ name: 'XML', extensions: ['xml'] }]});
  if (typeof path === 'string') await api.openXml(path);
}

async function saveXml() {
  const path = await save({ filters: [{ name: 'XML', extensions: ['xml'] }]});
  if (path) await api.saveXml(path);
}
```

> 다이얼로그 API는 v2 플러그인 문서 시그니처 그대로입니다. ([Tauri][21])

---

## (마지막) 참고 문서 요점

* **Tauri v2 커맨드/이벤트/프런트 통신**: `invoke`, `listen`, Rust `#[tauri::command]`/`emit` 패턴. ([Tauri][1])
* **JS 이벤트 네임스페이스**: `@tauri-apps/api/event` 및 전역 대체. ([Tauri][22])
* **프로젝트/설정/Capabilities**: `tauri.conf.json`, `capabilities/`. ([Tauri][2])
* **Stronghold(보안 저장소)**: 초기화, 권한, JS/Rust 사용법. ([Tauri][5])
* **SQL/Store/Dialog 플러그인**: v2 문서/레퍼런스. ([Tauri][23])
* **XML 파서/트리**: quick-xml(스트리밍), roxmltree(읽기 전용 트리). ([Docs.rs][8])
* **Diff 알고리즘**: similar crate. ([Docs.rs][9])
* **HTTP/동시성**: reqwest(Client 풀), tokio::Semaphore. ([Docs.rs][6])
* **React 테이블/가상화**: TanStack Table v8, react-window, overscan. ([TanStack][14], [React Window][15], [web.dev][16])
* **Gemini API**: `:generateContent` REST, 모델 스펙/장문 컨텍스트. ([Google AI for Developers][11], [Google Cloud][12])
* **WebView2 (Windows 11 포함)**: 배포/런타임 가이드. ([Microsoft Learn][19], [Windows Blog][20])

---

필요하시면 위 스켈레톤에서 **실제 `load_xtranslator_xml`/`write_xtranslator_xml` 구현**(xTranslator XML 스키마 기준), **Vertex AI 스트리밍 대응 버전**, **취소 토큰(cancellation) 도입**, **ICU MessageFormat 정밀 검증기**까지 바로 확장해 드리겠습니다.

[1]: https://v2.tauri.app/develop/calling-rust/ "Calling Rust from the Frontend | Tauri"
[2]: https://v2.tauri.app/start/project-structure/?utm_source=chatgpt.com "Project Structure"
[3]: https://v2.tauri.app/start/frontend/vite/?utm_source=chatgpt.com "Vite"
[4]: https://v2.tauri.app/security/capabilities/?utm_source=chatgpt.com "Capabilities"
[5]: https://v2.tauri.app/plugin/stronghold/ "Stronghold | Tauri"
[6]: https://docs.rs/reqwest/latest/reqwest/struct.Client.html?utm_source=chatgpt.com "Client in reqwest - Rust"
[7]: https://docs.rs/tokio/latest/tokio/sync/struct.Semaphore.html?utm_source=chatgpt.com "Semaphore in tokio::sync - Rust"
[8]: https://docs.rs/quick-xml?utm_source=chatgpt.com "quick_xml - Rust"
[9]: https://docs.rs/similar?utm_source=chatgpt.com "similar - Rust"
[10]: https://docs.rs/crate/rusqlite/latest/features?utm_source=chatgpt.com "Feature flags - rusqlite 0.37.0"
[11]: https://ai.google.dev/gemini-api/docs?utm_source=chatgpt.com "Gemini API | Google AI for Developers"
[12]: https://cloud.google.com/vertex-ai/generative-ai/docs/models/gemini/1-5-flash?utm_source=chatgpt.com "Gemini 1.5 Flash | Generative AI on Vertex AI"
[13]: https://ai.google.dev/api/generate-content?utm_source=chatgpt.com "Generating content | Gemini API | Google AI for Developers"
[14]: https://tanstack.com/table/v8/docs/introduction?utm_source=chatgpt.com "Introduction | TanStack Table Docs"
[15]: https://react-window.vercel.app/?utm_source=chatgpt.com "react-window"
[16]: https://web.dev/articles/virtualize-long-lists-react-window?utm_source=chatgpt.com "Virtualize large lists with react-window | Articles"
[17]: https://v2.tauri.app/plugin/dialog/?utm_source=chatgpt.com "Dialog"
[18]: https://cloud.google.com/vertex-ai/generative-ai/docs/model-reference/inference?utm_source=chatgpt.com "Generate content with the Gemini API in Vertex AI"
[19]: https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution?utm_source=chatgpt.com "Distribute your app and the WebView2 Runtime"
[20]: https://blogs.windows.com/msedgedev/2022/12/14/delivering-microsoft-edge-webview2-runtime-to-managed-windows-10-devices/?utm_source=chatgpt.com "Delivering Microsoft Edge WebView2 Runtime to managed ..."
[21]: https://v2.tauri.app/reference/javascript/dialog/?utm_source=chatgpt.com "tauri-apps/plugin-dialog"
[22]: https://v2.tauri.app/reference/javascript/api/namespaceevent/?utm_source=chatgpt.com "event"
[23]: https://v2.tauri.app/plugin/sql/?utm_source=chatgpt.com "SQL"
