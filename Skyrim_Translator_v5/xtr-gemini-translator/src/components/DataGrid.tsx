import * as React from 'react'
import {
  ColumnDef,
  getCoreRowModel,
  useReactTable,
  flexRender,
  SortingState,
  getSortedRowModel,
} from '@tanstack/react-table'
import { useVirtualizer } from '@tanstack/react-virtual'
import shallow from 'zustand/shallow'
import { useStore } from '../state/store'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from './ui/table'
import { Card } from './ui/card'
import { Input } from './ui/input'
import { ArrowUp, ArrowDown, ChevronsUpDown } from 'lucide-react'
const columns: ColumnDef<any>[] = [
  { header: 'ID', accessorKey: 'id', size: 80, enableSorting: true },
  { header: '상태', accessorKey: 'status', size: 90, enableSorting: true },
  { header: '원문', accessorKey: 'src', enableSorting: false },
  { header: '번역', accessorKey: 'dst', enableSorting: false },
]
export function DataGrid() {
  const rowsData = useStore(s => s.filteredRows, shallow)
  const highlightRowId = useStore(s => s.highlightRowId)
  const scrollToRowId = useStore(s => s.scrollToRowId)
  const selectedRowId = useStore(s => s.selectedRowId)
  const selectRow = useStore(s => s.actions.selectRow)
  const [sorting, setSorting] = React.useState<SortingState>([])
  const [query, setQuery] = React.useState('')
  const viewData = React.useMemo(() => {
    const q = query.trim().toLowerCase()
    if (!q) return rowsData
    return rowsData.filter(r =>
      String(r.id).includes(q) ||
      (r.src?.toLowerCase().includes(q)) ||
      (r.dst?.toLowerCase().includes(q))
    )
  }, [rowsData, query])
  const [columnSizing, setColumnSizing] = React.useState<Record<string, number>>({})
  const table = useReactTable({
    data: viewData,
    columns,
    state: { sorting, columnSizing },
    onSortingChange: setSorting,
    onColumnSizingChange: setColumnSizing,
    columnResizeMode: 'onChange',
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
  })
  const parentRef = React.useRef<HTMLDivElement>(null)
  const rows = table.getRowModel().rows
  const rowVirtualizer = useVirtualizer({
    count: rows.length,
    getScrollElement: () => parentRef.current,
    estimateSize: () => 40,
    overscan: 8,
  })
  React.useEffect(() => {
    if (!scrollToRowId) return
    const idx = viewData.findIndex(r => r.id === scrollToRowId)
    if (idx >= 0) rowVirtualizer.scrollToIndex(idx, { align: 'center' })
  }, [scrollToRowId, viewData])
  return (
    <Card className="h-full overflow-hidden">
      <div className="border-b p-2">
        <Input placeholder="검색: ID/원문/번역" value={query} onChange={e => setQuery(e.target.value)} />
      </div>
      <div ref={parentRef} className="h-[calc(100%-42px)] overflow-auto">
        <Table className="relative">
          <TableHeader className="sticky top-0 z-10" style={{ backgroundColor: 'var(--muted)' }}>
            {table.getHeaderGroups().map((hg) => (
              <TableRow key={hg.id}>
                {hg.headers.map((h) => {
                  const canSort = h.column.getCanSort()
                  const sorted = h.column.getIsSorted()
                  const resizeHandler = h.getResizeHandler()
                  return (
                    <TableHead key={h.id} style={{ width: h.getSize(), position: 'relative', userSelect: 'none' }}>
                      {h.isPlaceholder ? null : (
                        <div className="flex items-center gap-1">
                          <button
                            className="flex items-center gap-1"
                            onClick={canSort ? h.column.getToggleSortingHandler() : undefined}
                            title={canSort ? '정렬' : undefined}
                          >
                            {flexRender(h.column.columnDef.header, h.getContext())}
                            {canSort ? (
                              sorted === 'asc' ? <ArrowUp className="h-3.5 w-3.5" /> :
                              sorted === 'desc' ? <ArrowDown className="h-3.5 w-3.5" /> :
                              <ChevronsUpDown className="h-3.5 w-3.5 opacity-50" />
                            ) : null}
                          </button>
                          <div
                            onMouseDown={resizeHandler}
                            onTouchStart={resizeHandler}
                            className={`ml-auto h-4 w-1 cursor-col-resize select-none rounded-sm bg-[var(--border)] ${h.column.getIsResizing() ? 'bg-[var(--primary)]' : ''}`}
                            title="열 너비 조절"
                          />
                        </div>
                      )}
                    </TableHead>
                  )
                })}
              </TableRow>
            ))}
          </TableHeader>
          <TableBody style={{ display: 'grid', height: `${rowVirtualizer.getTotalSize()}px`, position: 'relative' }}>
            {rowVirtualizer.getVirtualItems().map((vr) => {
              const row = rows[vr.index]
              const id = row.getValue('id') as number
              const isSelected = id === selectedRowId
              const altBg = vr.index % 2 === 1 ? 'color-mix(in oklch, var(--muted) 35%, transparent)' : undefined
              const bg = id === highlightRowId
                ? 'color-mix(in oklch, var(--primary) 12%, var(--background))'
                : isSelected
                ? 'color-mix(in oklch, var(--primary) 8%, var(--background))'
                : altBg
              return (
                <TableRow
                  key={row.id}
                  data-index={vr.index}
                  className="absolute w-full"
                  style={{
                    transform: `translateY(${vr.start}px)`,
                    background: bg,
                    cursor: 'pointer',
                    borderLeft: isSelected ? '3px solid var(--primary)' : undefined,
                  }}
                  onClick={() => selectRow(id)}
                >
                  {row.getVisibleCells().map((cell) => (
                    <TableCell key={cell.id} style={{ width: cell.column.getSize() }}>
                      {flexRender(cell.column.columnDef.cell, cell.getContext())}
                    </TableCell>
                  ))}
                </TableRow>
              )
            })}
          </TableBody>
        </Table>
      </div>
    </Card>
  )
}
