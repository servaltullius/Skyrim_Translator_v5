X-TR Fluent Skin Integration
============================
- App location: `xtr-gemini-translator/`
- Theme: OKLCH tokens + dark mode class in `src/index.css`
- UI: TopMenubar + Sidebar + DataGrid (TanStack Virtual) + Editors
- Tauri: Existing commands/events remain wired through `src/api/tauri`
- Build: `cd xtr-gemini-translator && npm i && npm run dev` (or `npm run tauri dev`)
+
Replacing stub UI with shadcn/ui (recommended)
- Run once:
  - npx shadcn@latest init
  - npx shadcn@latest add button menubar sidebar table scroll-area resizable progress dropdown-menu input select label switch textarea sonner separator card badge
- Generates `src/components/ui/*` components used by the app.
+
Notes
- The root Vite sample was removed to avoid duplication. The app builds from `xtr-gemini-translator/` only.
- To adjust accent, tweak `--primary` and `--accent` tokens in `src/index.css`.
