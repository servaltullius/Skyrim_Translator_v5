import { ScrollArea } from "@/components/ui/scroll-area";
import { Card } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Button } from "@/components/ui/button";
export function InspectorPanel() {
  return (
    <ScrollArea className="h-full">
      <div className="flex flex-col gap-3 p-3">
        <Card className="space-y-2 p-4">
          <div className="text-sm font-semibold">세부 정보</div>
          <div className="text-xs" style={{ color: '#555' }}>행을 선택하면 여기에 편집할 수 있습니다.</div>
          <Label>원문</Label>
          <Textarea rows={6} readOnly placeholder="선택한 원문이 표시됩니다." />
          <Label>번역문</Label>
          <Textarea rows={8} placeholder="여기에 번역문을 편집..." />
          <div className="flex gap-2">
            <Button size="sm">적용</Button>
            <Button size="sm" variant="outline">되돌리기</Button>
          </div>
        </Card>
      </div>
    </ScrollArea>
  );
}
