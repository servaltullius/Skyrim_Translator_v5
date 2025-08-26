import { useMemo, useState, useEffect, useRef } from 'react'
import { useStore } from '../state/store'
import { edit } from '../api/tauri'
import { ScrollArea } from './ui/scroll-area'
import { Card } from './ui/card'
import { Label } from './ui/label'
import { Textarea } from './ui/textarea'
import { Button } from './ui/button'
export function Editors() {
  const { selectedRowId, rows, actions, errorMap } = useStore()
  const row = useMemo(() => rows.find(r => r.id === selectedRowId) || null, [rows, selectedRowId])
  const [dst, setDst] = useState<string>('')
  const dstRef = useRef<HTMLTextAreaElement>(null)
  useEffect(() => {
    if (!row) return
    const err = errorMap[row.id]
    if (!row.dst && err?.preview) {
      setDst(err.preview)
      // schedule selection at position
      setTimeout(() => {
        if (dstRef.current && typeof err.pos === 'number') {
          const p = Math.min(err.pos, err.preview!.length)
          dstRef.current.focus()
          dstRef.current.setSelectionRange(p, Math.min(p + 8, err.preview!.length))
        }
      }, 50)
    } else {
      setDst(row.dst ?? '')
    }
  }, [row?.id])
  if (!row) {
    return (
      <ScrollArea className="h-full">
        <div className="p-3 text-sm" style={{ color: '#666' }}>행을 선택하면 여기서 내용을 편집할 수 있습니다.</div>
      </ScrollArea>
    )
  }
  const onSave = async () => {
    await edit.updateEntryDst(row.id, dst)
    actions.updateRow(row.id, dst)
    actions.addLog(`[row] id=${row.id} saved`)
  }
  const onClear = async () => {
    await edit.clearEntryDst(row.id)
    actions.setRowPending(row.id)
    actions.addLog(`[row] id=${row.id} cleared`)
    setDst('')
  }
  return (
    <ScrollArea className="h-full">
      <div className="flex flex-col gap-3 p-3">
        <Card className="space-y-2 p-3">
          <div className="text-sm font-semibold">행 편집</div>
          <div className="text-xs" style={{ color: '#555' }}>ID: {row.id} • 상태: {row.status}</div>
          <Label>원문</Label>
          <Textarea readOnly rows={6} value={row.src} />
          <Label>번역</Label>
          <Textarea ref={dstRef} rows={8} value={dst} onChange={e => setDst(e.target.value)} />
          <div className="flex gap-2">
            <Button size="sm" onClick={onSave}>저장</Button>
            <Button size="sm" variant="outline" onClick={onClear}>초기화(미번역)</Button>
          </div>
        </Card>
      </div>
    </ScrollArea>
  )
}
