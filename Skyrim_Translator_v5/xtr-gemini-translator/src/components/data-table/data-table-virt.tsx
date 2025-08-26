import * as React from "react";
import {
  useReactTable,
  getCoreRowModel,
  flexRender,
} from "@tanstack/react-table";
import { useVirtualizer } from "@tanstack/react-virtual";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Card } from "@/components/ui/card";
type Row = {
  id: string;
  status: "미번역" | "완료" | "오류";
  source: string;   // 원문
  target: string;   // 번역문
};
const columns = [
  { accessorKey: "id", header: "ID", size: 96 },
  { accessorKey: "status", header: "상태", size: 120 },
  { accessorKey: "source", header: "원문" },
  { accessorKey: "target", header: "번역" },
] as const;
function makeRows(n = 30000): Row[] {
  return Array.from({ length: n }, (_, i) => ({
    id: String(i + 1),
    status: i % 11 === 0 ? "오류" : i % 7 === 0 ? "완료" : "미번역",
    source: `This is a sample source text #${i + 1}`,
    target: "",
  }));
}
export function DataTableVirt() {
  const data = React.useMemo(() => makeRows(30000), []);
  const table = useReactTable({
    data,
    columns: columns as any,
    getCoreRowModel: getCoreRowModel(),
  });
  const parentRef = React.useRef<HTMLDivElement>(null);
  const rows = table.getRowModel().rows;
  const rowVirtualizer = useVirtualizer({
    count: rows.length,
    getScrollElement: () => parentRef.current,
    estimateSize: () => 44,
    overscan: 8,
  });
  return (
    <Card className="h-full overflow-hidden">
      <div ref={parentRef} className="h-full overflow-auto">
        <Table className="relative">
          <TableHeader className="sticky top-0 z-10 bg-background">
            {table.getHeaderGroups().map((hg) => (
              <TableRow key={hg.id}>
                {hg.headers.map((h) => (
                  <TableHead key={h.id} style={{ width: h.getSize?.() }}>
                    {(h as any).isPlaceholder ? null : flexRender((h as any).column.columnDef.header, (h as any).getContext())}
                  </TableHead>
                ))}
              </TableRow>
            ))}
          </TableHeader>
          <TableBody
            style={{
              display: "grid",
              height: `${rowVirtualizer.getTotalSize()}px`,
              position: "relative",
            }}
          >
            {rowVirtualizer.getVirtualItems().map((vr) => {
              const row = rows[vr.index];
              return (
                <TableRow
                  key={row.id}
                  data-index={vr.index}
                  className="absolute w-full"
                  style={{ transform: `translateY(${vr.start}px)` }}
                >
                  {row.getVisibleCells().map((cell) => (
                    <TableCell key={cell.id} style={{ width: (cell.column as any).getSize?.() }}>
                      {flexRender(cell.column.columnDef.cell, cell.getContext())}
                    </TableCell>
                  ))}
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </div>
    </Card>
  );
}
