import {
  Sidebar,
  SidebarContent,
  SidebarHeader,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupLabel,
  SidebarGroupContent,
  SidebarMenu,
  SidebarMenuItem,
  SidebarMenuButton,
} from "@/components/ui/sidebar";
import { Home, Settings, Book, WandSparkles, ListChecks } from "lucide-react";
const items = [
  { title: "프로젝트", icon: Home },
  { title: "설정", icon: Settings },
  { title: "용어집", icon: Book },
  { title: "커스텀 프롬프트", icon: WandSparkles },
  { title: "로그", icon: ListChecks },
];
export function AppSidebar() {
  return (
    <Sidebar collapsible="icon">
      <SidebarHeader />
      <SidebarContent>
        <SidebarGroup>
          <SidebarGroupLabel>메뉴</SidebarGroupLabel>
          <SidebarGroupContent>
            <SidebarMenu>
              {items.map((it) => (
                <SidebarMenuItem key={it.title}>
                  <SidebarMenuButton asChild>
                    <button type="button" title={it.title} className="w-full justify-start">
                      <it.icon className="mr-2 h-4 w-4" />
                      <span>{it.title}</span>
                    </button>
                  </SidebarMenuButton>
                </SidebarMenuItem>
              ))}
            </SidebarMenu>
          </SidebarGroupContent>
        </SidebarGroup>
      </SidebarContent>
      <SidebarFooter />
    </Sidebar>
  );
}
