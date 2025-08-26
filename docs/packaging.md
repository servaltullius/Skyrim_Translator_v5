# Packaging (Windows 11 MSI)
 
## Prereqs
- Rust toolchain (MSVC), Node.js (LTS), Tauri CLI v2.
- Windows 11 with WebView2 Runtime (preinstalled).
 
## Icons
- Place a valid `.ico` at `xtr-gemini-translator/icons/app.ico` (multi-size recommended).
- `src-tauri/tauri.conf.json` already references `../icons/app.ico`.
 
## Build
1) `cd xtr-gemini-translator`
2) `npm i`
3) `npm run build`
4) `npm run tauri build`
 
Artifacts are created under `xtr-gemini-translator/src-tauri/target/release/bundle/msi/`.
 
## WebView2 Check
- Windows 11 includes WebView2 by default.
- If targeting older Windows, instruct users to install WebView2 Runtime from Microsoft or ship a bootstrapper.
 
## Versioning
- Update `version` in `src-tauri/tauri.conf.json` for releases.
- Maintain `docs/release_notes.md`.
 
