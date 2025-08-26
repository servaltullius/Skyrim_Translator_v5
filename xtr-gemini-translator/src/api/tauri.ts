// Safe Tauri wrappers with browser fallbacks for Vite dev (non-Tauri).
let isTauri = false
try {
  // Detect Tauri runtime presence
  isTauri =
    typeof window !== 'undefined' &&
    (('__TAURI_INTERNALS__' in (window as any)) || ('__TAURI__' in (window as any)))
} catch {}
type Unlisten = () => void
// Browser fallback memory
const mem = {
  settings: {
    concurrency: 10, batch_size: 8, rps: 5, rate_mode: 'gap', bucket_capacity: 10,
    model: 'gemini-1.5-flash', temperature: 0.2, custom_prompt: '', mock_llm: false, redact_logs: false,
  },
  glossary: [] as any[],
  rows: [] as any[],
}
function fakeRows(n: number) {
  if (mem.rows.length >= n) return mem.rows
  mem.rows = Array.from({ length: n }, (_, i) => ({
    id: i + 1,
    status: i % 11 === 0 ? 'error' : i % 7 === 0 ? 'done' : 'pending',
    src: `Sample source #${i + 1}`,
    dst: '',
  }))
  return mem.rows
}
export const api = isTauri ? (() => {
  const { invoke } = require('@tauri-apps/api/core')
  return {
    openXml: (path: string) => invoke<number>('open_xml', { path }),
    getRows: (limit?: number, offset?: number) => invoke<any[]>('get_rows', { limit, offset }),
    startTranslate: (opts?: any) => invoke<void>('start_translate', { opts }),
    cancelTranslate: () => invoke<boolean>('cancel_translate'),
    saveXml: (path?: string) => invoke<number>('save_xml', { path }),
    getGlossary: () => invoke<any>('get_glossary'),
    upsertGlossary: (json: any) => invoke<void>('upsert_glossary', { json }),
    getSettings: () => invoke<any>('get_settings'),
    updateSettings: (patch: any) => invoke<void>('update_settings', { patch }),
    setApiKeyOnce: (apiKey?: string) => invoke<{ stored: boolean }>('set_api_key_once', { input: { apiKey } }),
  }
})() : {
  openXml: async (_path: string) => fakeRows(30000).length,
  getRows: async (limit?: number, offset?: number) => {
    const src = fakeRows(30000)
    const off = offset ?? 0
    const lim = limit ?? src.length
    return src.slice(off, off + lim)
  },
  startTranslate: async () => {},
  cancelTranslate: async () => true,
  saveXml: async (_path?: string) => 1,
  getGlossary: async () => mem.glossary,
  upsertGlossary: async (json: any) => { mem.glossary = json },
  getSettings: async () => mem.settings,
  updateSettings: async (patch: any) => { mem.settings = { ...mem.settings, ...patch } },
  setApiKeyOnce: async (_apiKey?: string) => ({ stored: true }),
}
export const events = isTauri ? (() => {
  const { listen } = require('@tauri-apps/api/event')
  return {
    onRowTranslated: (cb: (p: any) => void) => listen('row_translated', (e: any) => cb(e.payload)) as Promise<Unlisten>,
    onProgress: (cb: (p: any) => void) => listen('job_progress', (e: any) => cb(e.payload)) as Promise<Unlisten>,
    onError: (cb: (p: any) => void) => listen('job_error', (e: any) => cb(e.payload)) as Promise<Unlisten>,
    onDone: (cb: (p: any) => void) => listen('job_done', (e: any) => cb(e.payload)) as Promise<Unlisten>,
    onMetrics: (cb: (p: any) => void) => listen('job_metrics', (e: any) => cb(e.payload)) as Promise<Unlisten>,
  }
})() : {
  onRowTranslated: async (_cb: (p: any) => void) => (() => {}) as Unlisten,
  onProgress: async (_cb: (p: any) => void) => (() => {}) as Unlisten,
  onError: async (_cb: (p: any) => void) => (() => {}) as Unlisten,
  onDone: async (_cb: (p: any) => void) => (() => {}) as Unlisten,
  onMetrics: async (_cb: (p: any) => void) => (() => {}) as Unlisten,
}
export const edit = isTauri ? (() => {
  const { invoke } = require('@tauri-apps/api/core')
  return {
    updateEntryDst: (id: number, dst: string) => invoke<void>('update_entry_dst', { id, dst }),
    clearEntryDst: (id: number) => invoke<void>('clear_entry_dst', { id }),
  }
})() : {
  updateEntryDst: async (id: number, dst: string) => {
    const r = mem.rows.find(r => r.id === id); if (r) { r.dst = dst; r.status = 'done' }
  },
  clearEntryDst: async (id: number) => {
    const r = mem.rows.find(r => r.id === id); if (r) { r.dst = ''; r.status = 'pending' }
  },
}
export const files = isTauri ? (() => {
  const { invoke } = require('@tauri-apps/api/core')
  return {
    exportErrors: (path: string, data: any) => invoke<boolean>('export_errors', { path, data }),
  }
})() : {
  exportErrors: async (_path: string, _data: any) => true,
}
