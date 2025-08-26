import React from 'react'
import ReactDOM from 'react-dom/client'
import { App } from './App'
import './index.css'
import { events } from './api/tauri'
import { useStore } from './state/store'
;(async () => {
  // Subscribe to tauri events and wire to store
  const unsubs = await Promise.all([
    events.onRowTranslated((p: any) => {
      useStore.getState().actions.updateRow(p.id, p.dst)
      useStore.getState().actions.addLog(`[row] id=${p.id} status=done`)
    }),
    events.onProgress((p: any) => {
      useStore.getState().actions.setProgress(p.done, p.total)
    }),
    events.onError((p: any) => {
      // mark error row if id present
      if (p?.id) useStore.getState().actions.markError(p.id, { code: p.code || 'error', pos: p.pos, preview: p.preview })
      useStore.getState().actions.addLog(`[err] id=${p?.id ?? '-'} code=${p?.code ?? ''} ${p?.message ?? ''}`.trim())
    }),
    events.onDone((p: any) => {
      useStore.getState().actions.addLog(`[done] ${p?.done}/${p?.total}`)
    }),
    events.onMetrics((m: any) => {
      useStore.getState().actions.setMetrics({ durationMs: m.duration_ms ?? m.durationMs ?? 0, rps: m.rps ?? 0, p50Ms: m.p50_ms ?? m.p50Ms ?? 0, p95Ms: m.p95_ms ?? m.p95Ms ?? 0 })
    }),
  ])
  // Mount React app
  ReactDOM.createRoot(document.getElementById('root')!).render(
    <React.StrictMode>
      <App />
    </React.StrictMode>,
  )
  // Expose cleanup in dev HMR scenario
  if (import.meta && (import.meta as any).hot) {
    ;(import.meta as any).hot.dispose(() => {
      unsubs.forEach((u) => u())
    })
  }
})()
