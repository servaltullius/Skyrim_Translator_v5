import { createWithEqualityFn } from 'zustand/traditional'
type Row = { id: number; status: 'pending' | 'done' | 'error'; src: string; dst?: string }
type ErrorInfo = { code: string; pos?: number; preview?: string }
type State = {
  rows: Row[]
  filteredRows: Row[]
  progress: { done: number; total: number }
  filter: 'all' | 'pending' | 'done' | 'error'
  logs: string[]
  highlightRowId: number | null
  scrollToRowId: number | null
  selectedRowId: number | null
  errorMap: Record<number, ErrorInfo>
  metrics?: { durationMs: number; rps: number; p50Ms: number; p95Ms: number }
  actions: {
    setRows: (rows: Row[]) => void
    updateRow: (id: number, dst: string) => void
    setProgress: (done: number, total: number) => void
    setFilter: (f: State['filter']) => void
    markError: (id: number, info: ErrorInfo) => void
    addLog: (line: string) => void
    clearLogs: () => void
    focusRow: (id: number) => void
    selectRow: (id: number) => void
    setRowPending: (id: number) => void
    setMetrics: (m: { durationMs: number; rps: number; p50Ms: number; p95Ms: number }) => void
  }
}
export const useStore = createWithEqualityFn<State>()((set, get) => ({
  rows: [],
  filteredRows: [],
  progress: { done: 0, total: 0 },
  filter: 'all',
  logs: [],
  highlightRowId: null,
  scrollToRowId: null,
  selectedRowId: null,
  errorMap: {},
  actions: {
    setRows: (rows) => {
      const f = get().filter
      const filtered = rows.filter(r => f === 'all' ? true : r.status === f)
      set({ rows, filteredRows: filtered })
    },
    updateRow: (id, dst) => {
      const rows = get().rows.map(r => r.id === id ? { ...r, dst, status: 'done' } : r)
      const { filter } = get()
      const filteredRows = rows.filter(r => filter === 'all' ? true : r.status === filter)
      set({ rows, filteredRows })
    },
    setProgress: (done, total) => set({ progress: { done, total } }),
    setFilter: (f) => {
      const rows = get().rows
      const filteredRows = rows.filter(r => f === 'all' ? true : r.status === f)
      set({ filter: f, filteredRows })
    },
    markError: (id, info) => {
      const rows = get().rows.map(r => r.id === id ? { ...r, status: 'error' } : r)
      const f = get().filter
      const filteredRows = rows.filter(r => f === 'all' ? true : r.status === f)
      const logs = [...get().logs, `[row-error] id=${id} code=${info.code}${info.pos !== undefined ? ` pos=${info.pos}` : ''}`]
      const errorMap = { ...get().errorMap, [id]: info }
      set({ rows, filteredRows, logs, errorMap })
    },
    addLog: (line) => {
      const logs = [...get().logs, line]
      set({ logs })
    },
    clearLogs: () => set({ logs: [] }),
    focusRow: (id) => {
      // set both highlight and scroll target; consumer will scroll on change
      set({ highlightRowId: id, scrollToRowId: id })
      // remove highlight after a delay (visual pulse can be handled by CSS; keep simple here)
      setTimeout(() => {
        if (get().highlightRowId === id) {
          set({ highlightRowId: null })
        }
      }, 2000)
    },
    selectRow: (id) => set({ selectedRowId: id }),
    setRowPending: (id) => {
      const rows = get().rows.map(r => r.id === id ? { ...r, dst: undefined, status: 'pending' } : r)
      const f = get().filter
      const filteredRows = rows.filter(r => f === 'all' ? true : r.status === f)
      set({ rows, filteredRows })
    }
    ,
    setMetrics: (m) => set({ metrics: { durationMs: m.durationMs, rps: m.rps, p50Ms: m.p50Ms, p95Ms: m.p95Ms } })
  },
})) 
