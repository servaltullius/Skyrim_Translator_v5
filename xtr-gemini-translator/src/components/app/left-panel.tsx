import { ScrollArea } from "@/components/ui/scroll-area";
import { Card } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";
import { Select, SelectTrigger, SelectContent, SelectItem, SelectValue } from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";
import { Button } from "@/components/ui/button";
import { Separator } from "@/components/ui/separator";
import { Badge } from "@/components/ui/badge";
export function LeftPanel() {
  return (
    <ScrollArea className="h-full">
      <div className="flex flex-col gap-3 p-3">
        <Card className="p-4">
          <div className="mb-2 text-sm font-semibold">필터</div>
          <div className="flex flex-wrap items-center gap-2">
            <Badge>전체 0</Badge>
            <Badge>미번역 0</Badge>
            <Badge>완료 0</Badge>
            <Badge>오류 0</Badge>
          </div>
        </Card>
        <Card className="space-y-3 p-4">
          <div className="text-sm font-semibold">처리 설정</div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <Label>동시성</Label>
              <Input placeholder="10" />
            </div>
            <div>
              <Label>배치 크기</Label>
              <Input placeholder="8" />
            </div>
            <div className="col-span-2">
              <Label>모델</Label>
              <Select defaultValue="gemini-1.5-flash">
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="gemini-1.5-flash">gemini-1.5-flash</SelectItem>
                  <SelectItem value="gemini-2.0-flash">gemini-2.0-flash</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="col-span-2 flex items-center justify-between rounded-md border p-2">
              <div className="text-sm">민감 로그 차단</div>
              <Switch />
            </div>
            <div className="col-span-2 flex gap-2">
              <Button size="sm">설정 저장</Button>
              <Button size="sm" variant="outline">오류 내보내기</Button>
            </div>
          </div>
        </Card>
        <Card className="space-y-2 p-4">
          <div className="text-sm font-semibold">커스텀 프롬프트</div>
          <Textarea rows={5} placeholder="추가 지침을 입력하세요" />
        </Card>
        <Card className="space-y-2 p-4">
          <div className="text-sm font-semibold">용어집(JSON)</div>
          <Textarea rows={8} placeholder='[{"key":"Dragonborn","value":"드래곤본"}]' />
          <div className="flex gap-2">
            <Button size="sm">용어집 저장</Button>
            <Button size="sm" variant="outline">초기화</Button>
          </div>
        </Card>
        <Card className="p-4">
          <div className="mb-2 text-sm font-semibold">로그</div>
          <div className="text-xs" style={{ color: '#888' }}>로그 없음</div>
        </Card>
        <Separator />
        <div className="pb-6" />
      </div>
    </ScrollArea>
  );
}
