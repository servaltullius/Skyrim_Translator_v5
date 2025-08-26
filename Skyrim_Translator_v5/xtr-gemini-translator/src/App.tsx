import { useState } from 'react'
import { api } from './api/tauri'
import { useStore } from './state/store'
import { open, save } from '@tauri-apps/plugin-dialog'
import { ThemeProvider } from './components/theme-provider'
import { Toaster } from './components/ui/sonner'
import { TopMenubar } from './components/app/top-menubar'
import { SidebarProvider, SidebarInset } from './components/ui/sidebar'
import { AppSidebar } from './components/app/app-sidebar'
import { ResizableHandle, ResizablePanel, ResizablePanelGroup } from './components/ui/resizable'
import { Sidebar as LeftPanel } from './components/Sidebar'
import { Editors } from './components/Editors'
import { DataGrid } from './components/DataGrid'
import { Progress } from './components/ui/progress'
export function App() {
  const [busy, setBusy] = useState(false)
  const { progress, metrics } = useStore()
  const onSetKey = async () => {
    const input = window.prompt('Gemini API Key를 입력하세요 (백엔드로만 저장됩니다).')
    if (input && input.trim().length > 0) {
      await api.setApiKeyOnce(input.trim())
    }
  }
  const onOpen = async () => {
    const path = await open({ filters: [{ name: 'XML', extensions: ['xml'] }] })
    if (typeof path === 'string') {
      const count = await api.openXml(path)
      const rows = await api.getRows(Math.min(5000, count), 0)
      useStore.getState().actions.setRows(rows)
      useStore.getState().actions.setProgress(0, count)
    }
  }
  const onSave = async () => {
    const path = await save({ filters: [{ name: 'XML', extensions: ['xml'] }] })
    if (path) await api.saveXml(path as string)
  }
  const onStart = async () => {
    setBusy(true)
    try {
      await api.startTranslate()
    } finally {
      setBusy(false)
    }
  }
  const onCancel = async () => {
    await api.cancelTranslate()
  }
  return (
    <ThemeProvider defaultTheme="dark" storageKey="vite-ui-theme">
      <div className="flex h-screen min-h-0 w-full">
        <SidebarProvider>
          <AppSidebar />
          <SidebarInset>
            <TopMenubar
              onSetKey={onSetKey}
              onOpen={onOpen}
              onSave={onSave}
              onStart={onStart}
              onCancel={onCancel}
              rightSlot={
                <div className="flex items-center gap-3 text-xs text-[var(--muted-foreground)]">
                  <span>진행: {progress.done}/{progress.total}</span>
                  {metrics && (
                    <span>
                      p50 {metrics.p50Ms}ms · p95 {metrics.p95Ms}ms · rps {metrics.rps.toFixed(1)}
                    </span>
                  )}
                </div>
              }
            />
            {progress.total > 0 && (
              <div className="px-3 py-1">
                <Progress value={(progress.done / Math.max(1, progress.total)) * 100} />
              </div>
            )}
            <div className="h-[calc(100vh-40px)] p-3">
              <ResizablePanelGroup direction="horizontal" className="rounded-lg border">
                <ResizablePanel defaultSize={24} minSize={18} maxSize={30}>
                  <LeftPanel />
                </ResizablePanel>
                <ResizableHandle withHandle />
                <ResizablePanel defaultSize={52} minSize={40}>
                  <div className="h-full min-w-0">
                    <DataGrid />
                  </div>
                </ResizablePanel>
                <ResizableHandle withHandle />
                <ResizablePanel defaultSize={24} minSize={18} maxSize={30}>
                  <Editors />
                </ResizablePanel>
              </ResizablePanelGroup>
            </div>
          </SidebarInset>
        </SidebarProvider>
      </div>
      <Toaster />
    </ThemeProvider>
  )
}
