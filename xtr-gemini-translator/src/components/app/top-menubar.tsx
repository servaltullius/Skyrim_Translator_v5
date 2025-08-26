import {
  Menubar,
  MenubarMenu,
  MenubarTrigger,
  MenubarContent,
  MenubarItem,
  MenubarSeparator,
} from "@/components/ui/menubar";
import { ModeToggle } from "@/components/mode-toggle";
import { ReactNode } from "react";
type Props = {
  onSetKey?: () => void;
  onOpen?: () => void;
  onSave?: () => void;
  onStart?: () => void;
  onCancel?: () => void;
  onValidate?: () => void;
  onExport?: () => void;
  rightSlot?: ReactNode;
};
export function TopMenubar({
  onSetKey,
  onOpen,
  onSave,
  onStart,
  onCancel,
  onValidate,
  onExport,
  rightSlot,
}: Props) {
  return (
    <div
      className="sticky top-0 z-50 border-b backdrop-blur-sm"
      style={{
        background: "color-mix(in oklch, var(--background) 92%, white)",
        boxShadow: "0 1px 2px oklch(0 0 0 / 0.06)",
        borderColor: "var(--border)",
      }}
    >
      <div className="mx-auto flex h-10 items-center justify-between px-3">
        <Menubar className="border-none shadow-none">
          <MenubarMenu>
            <MenubarTrigger>파일</MenubarTrigger>
            <MenubarContent>
              <MenubarItem onClick={onSetKey}>API 키 설정</MenubarItem>
              <MenubarItem onClick={onOpen}>열기...</MenubarItem>
              <MenubarItem onClick={onSave}>저장</MenubarItem>
              <MenubarSeparator />
              <MenubarItem onClick={onExport}>내보내기</MenubarItem>
            </MenubarContent>
          </MenubarMenu>
          <MenubarMenu>
            <MenubarTrigger>번역</MenubarTrigger>
            <MenubarContent>
              <MenubarItem onClick={onValidate}>검증 실행</MenubarItem>
              <MenubarItem onClick={onStart}>번역 시작</MenubarItem>
              <MenubarItem onClick={onCancel}>취소</MenubarItem>
            </MenubarContent>
          </MenubarMenu>
        </Menubar>
        <div className="flex items-center gap-2">
          {rightSlot}
          <ModeToggle />
        </div>
      </div>
    </div>
  );
}
