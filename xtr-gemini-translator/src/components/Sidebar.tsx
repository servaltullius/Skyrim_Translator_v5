import { useEffect, useMemo, useState } from 'react'
import { api, files } from '../api/tauri'
import { useStore } from '../state/store'
import { save } from '@tauri-apps/plugin-dialog'
import { ScrollArea } from './ui/scroll-area'
import { Card } from './ui/card'
import { Label } from './ui/label'
import { Input } from './ui/input'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from './ui/select'
import { Switch } from './ui/switch'
import { Textarea } from './ui/textarea'
import { Button } from './ui/button'
import { Separator } from './ui/separator'
import { Badge } from './ui/badge'
type Settings = {
  concurrency?: number
  batch_size?: number
  rps?: number
  rate_mode?: 'gap' | 'bucket'
  bucket_capacity?: number
  model?: string
  temperature?: number
  custom_prompt?: string
  mock_llm?: boolean
  redact_logs?: boolean
}
export function Sidebar() {
  const { filter, actions, logs } = useStore()
  const [settings, setSettings] = useState<Settings>({})
  const [glossaryText, setGlossaryText] = useState<string>('[]')
  const [glossaryDirty, setGlossaryDirty] = useState(false)
  const [logFilter, setLogFilter] = useState<'all' | 'err' | 'row' | 'done' | 'info'>('all')
  useEffect(() => {
    ;(async () => {
      // load settings
      const s = await api.getSettings()
      const normalized: Settings = {
        concurrency: s.concurrency ?? 10,
        batch_size: s.batch_size ?? 8,
        rps: s.rps ?? 5,
        rate_mode: (s.rate_mode as any) ?? 'gap',
        bucket_capacity: s.bucket_capacity ?? (2 * (s.rps ?? 5)),
        model: s.model ?? 'gemini-1.5-flash',
        temperature: s.temperature ?? 0.2,
        custom_prompt: s.custom_prompt ?? '',
        mock_llm: s.mock_llm ?? false,
        redact_logs: s.redact_logs ?? false,
      }
      setSettings(normalized)
      // load glossary
      try {
        const g = await api.getGlossary()
        setGlossaryText(JSON.stringify(g, null, 2))
      } catch {
        setGlossaryText('[]')
      }
    })()
  }, [])
  const onSaveSettings = async () => {
    await api.updateSettings({
      concurrency: Number(settings.concurrency ?? 10),
      batch_size: Number(settings.batch_size ?? 8),
      rps: Number(settings.rps ?? 5),
      rate_mode: settings.rate_mode ?? 'gap',
      bucket_capacity: Number(settings.bucket_capacity ?? (2 * (settings.rps ?? 5))),
      model: settings.model ?? 'gemini-1.5-flash',
      temperature: Number(settings.temperature ?? 0.2),
      custom_prompt: settings.custom_prompt ?? '',
      mock_llm: !!settings.mock_llm,
      redact_logs: !!settings.redact_logs,
    })
  }
  const onExportErrors = async () => {
    const path = await save({ filters: [{ name: 'JSON', extensions: ['json'] }] })
    if (!path) return
    const st = useStore.getState()
    const errs = Object.entries(st.errorMap).map(([id, info]) => {
      const row = st.rows.find(r => r.id === Number(id))
      return {
        id: Number(id),
        code: info.code,
        pos: info.pos ?? null,
        src: row?.src ?? null,
        dst: row?.dst ?? info.preview ?? null,
      }
    })
    await files.exportErrors(path as string, errs)
  }
  const onSaveGlossary = async () => {
    try {
      const json = JSON.parse(glossaryText)
      await api.upsertGlossary(json)
      setGlossaryDirty(false)
    } catch (e) {
      alert('용어집 JSON 파싱 오류: ' + (e as Error).message)
    }
  }
  const setF = (f: 'all' | 'pending' | 'done' | 'error') => actions.setFilter(f)
  const kindOf = (line: string): 'err' | 'row' | 'done' | 'info' => {
    if (line.startsWith('[err]') || line.startsWith('[row-error]')) return 'err'
    if (line.startsWith('[row]')) return 'row'
    if (line.startsWith('[done]')) return 'done'
    return 'info'
  }
  const filteredLogs = useMemo(() => {
    if (logFilter === 'all') return logs
    return logs.filter(l => kindOf(l) === logFilter)
  }, [logs, logFilter])
  const onLogClick = (line: string) => {
    const m = /id=(\d+)/.exec(line)
    if (m) {
      const id = Number(m[1])
      if (!Number.isNaN(id)) actions.focusRow(id)
    }
  }
  return (
    <ScrollArea className="h-full">
      <div className="flex flex-col gap-3 p-3">
        <StatsAndFilters />
        <Card className="space-y-3 p-3">
          <div className="text-sm font-semibold">처리 설정</div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <Label>동시성</Label>
              <Input type="number" min={1} max={64} value={settings.concurrency ?? 10}
                onChange={e => setSettings(s => ({ ...s, concurrency: Number(e.target.value) }))} />
            </div>
            <div>
              <Label>배치 크기</Label>
              <Input type="number" min={1} max={64} value={settings.batch_size ?? 8}
                onChange={e => setSettings(s => ({ ...s, batch_size: Number(e.target.value) }))} />
            </div>
            <div>
              <Label>RPS</Label>
              <Input type="number" min={1} max={50} value={settings.rps ?? 5}
                onChange={e => setSettings(s => ({ ...s, rps: Number(e.target.value) }))} />
            </div>
            <div>
              <Label>온도</Label>
              <Input type="number" step={0.1} min={0} max={1} value={settings.temperature ?? 0.2}
                onChange={e => setSettings(s => ({ ...s, temperature: Number(e.target.value) }))} />
            </div>
            <div className="col-span-2">
              <Label>모델</Label>
              <Input value={settings.model ?? 'gemini-1.5-flash'} onChange={e => setSettings(s => ({ ...s, model: e.target.value }))} />
            </div>
            <div>
              <Label>Rate 모드</Label>
              <Select defaultValue={settings.rate_mode ?? 'gap'}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="gap">간격(gap)</SelectItem>
                  <SelectItem value="bucket">토큰 버킷</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>버킷 용량</Label>
              <Input type="number" min={1} max={1000} value={settings.bucket_capacity ?? 10}
                onChange={e => setSettings(s => ({ ...s, bucket_capacity: Number(e.target.value) }))} />
            </div>
            <div className="col-span-2 flex items-center justify-between rounded-md border p-2">
              <div className="text-sm">모의 LLM</div>
              <Switch checked={!!settings.mock_llm} onChange={e => setSettings(s => ({ ...s, mock_llm: (e.target as HTMLInputElement).checked }))} />
            </div>
            <div className="col-span-2 flex items-center justify-between rounded-md border p-2">
              <div className="text-sm">민감 로그 차단</div>
              <Switch checked={!!settings.redact_logs} onChange={e => setSettings(s => ({ ...s, redact_logs: (e.target as HTMLInputElement).checked }))} />
            </div>
            <div className="col-span-2 flex gap-2">
              <Button size="sm" onClick={onSaveSettings}>설정 저장</Button>
              <Button size="sm" variant="outline" onClick={onExportErrors}>오류 내보내기</Button>
            </div>
          </div>
        </Card>
        <Card className="space-y-2 p-3">
          <div className="text-sm font-semibold">커스텀 프롬프트</div>
          <Textarea
            rows={5}
            value={settings.custom_prompt ?? ''}
            onChange={e => setSettings(s => ({ ...s, custom_prompt: e.target.value }))}
            placeholder="추가하고 싶은 지침을 입력하세요"
          />
        </Card>
        <Card className="space-y-2 p-3">
          <div className="text-sm font-semibold">용어집(JSON)</div>
          <Textarea
            rows={8}
            value={glossaryText}
            onChange={e => { setGlossaryText(e.target.value); setGlossaryDirty(true) }}
            placeholder='[ { "key": "Dragonborn", "value": "드래곤본" } ]'
          />
          <div className="flex gap-2">
            <Button size="sm" onClick={onSaveGlossary} disabled={!glossaryDirty}>용어집 저장</Button>
          </div>
        </Card>
        <Card className="p-3">
            <div className="mb-2 flex items-center justify-between">
              <div className="text-sm font-semibold">로그</div>
              <div className="flex gap-2">
              <select className="h-9 w-32 rounded-md border bg-[var(--background)] px-2 text-sm"
                value={logFilter} onChange={e => setLogFilter(e.target.value as any)}>
                <option value="all">전체</option>
                <option value="err">오류</option>
                <option value="row">행</option>
                <option value="done">완료</option>
                <option value="info">정보</option>
              </select>
              <Button size="sm" variant="outline" onClick={() => actions.clearLogs()}>지우기</Button>
              </div>
            </div>
          <div className="h-40 overflow-auto rounded-md border p-2" style={{ background: 'color-mix(in oklch, var(--background) 90%, white)' }}>
            {filteredLogs.length === 0 ? (
              <div className="text-xs" style={{ color: '#888' }}>로그 없음</div>
            ) : filteredLogs.slice(-200).map((l, i) => {
              const color = kindOf(l) === 'err' ? '#b00020' : kindOf(l) === 'done' ? '#0b6' : '#222'
              return (
                <div
                  key={i}
                  style={{ whiteSpace: 'pre-wrap', fontFamily: 'ui-monospace, monospace', fontSize: 12, color, cursor: 'pointer' }}
                  onClick={() => onLogClick(l)}
                  title="클릭하면 해당 행으로 스크롤합니다 (가능한 경우)"
                >
                  {l}
                </div>
              )
            })}
          </div>
        </Card>
        <Separator />
        <div className="pb-6" />
      </div>
    </ScrollArea>
  )
}
function StatsAndFilters() {
  const rows = useStore(s => s.rows)
  const filter = useStore(s => s.filter)
  const setFilter = useStore(s => s.actions.setFilter)
  const total = rows.length
  const pending = rows.filter(r => r.status === 'pending').length
  const done = rows.filter(r => r.status === 'done').length
  const error = rows.filter(r => r.status === 'error').length
  return (
    <Card className="p-4" style={{ boxShadow: '0 1px 3px oklch(0 0 0 / 0.08), 0 1px 2px oklch(0 0 0 / 0.06)' }}>
      <div className="mb-2 text-sm font-semibold">필터</div>
      <div className="flex flex-wrap items-center gap-2">
        <Button size="sm" variant={filter === 'all' ? 'default' : 'outline'} onClick={() => setFilter('all')}>
          전체 <span className="ml-2 rounded px-2 py-0.5 text-xs" style={{ background: 'var(--accent)' }}>{total}</span>
        </Button>
        <Button size="sm" variant={filter === 'pending' ? 'default' : 'outline'} onClick={() => setFilter('pending')}>
          미번역 <span className="ml-2 rounded px-2 py-0.5 text-xs" style={{ background: 'var(--accent)' }}>{pending}</span>
        </Button>
        <Button size="sm" variant={filter === 'done' ? 'default' : 'outline'} onClick={() => setFilter('done')}>
          완료 <span className="ml-2 rounded px-2 py-0.5 text-xs" style={{ background: 'var(--accent)' }}>{done}</span>
        </Button>
        <Button size="sm" variant={filter === 'error' ? 'default' : 'outline'} onClick={() => setFilter('error')}>
          오류 <span className="ml-2 rounded px-2 py-0.5 text-xs" style={{ background: 'var(--accent)' }}>{error}</span>
        </Button>
      </div>
    </Card>
  )
}
