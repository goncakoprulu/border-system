"use client";

import {
  Banknote, CalendarDays, ChevronRight, ClipboardCheck, GraduationCap, Home, LogOut,
  Menu, ReceiptText, Settings, ShieldCheck, UserCog, Users, WalletCards,
} from "lucide-react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useMemo, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { GlobalSearch } from "@/components/global-search";
import { Button } from "@/components/ui/button";
import { Separator } from "@/components/ui/separator";
import { Sheet, SheetContent, SheetTitle, SheetTrigger } from "@/components/ui/sheet";
import { useCurrentUser } from "@/hooks/use-current-user";
import { authApi } from "@/lib/api";

type NavItem = { label: string; href: string; icon: typeof Home; roles?: string[] };
const managementNav: NavItem[] = [
  { label: "Panel", href: "/dashboard", icon: Home },
  { label: "Öğrenciler", href: "/students", icon: Users, roles: ["Admin", "Management", "Reception"] },
  { label: "Sınıflar", href: "/classes", icon: GraduationCap, roles: ["Admin", "Management", "Reception"] },
  { label: "Program", href: "/schedule", icon: CalendarDays, roles: ["Admin", "Management", "Reception"] },
  { label: "Yoklama", href: "/attendance", icon: ClipboardCheck, roles: ["Admin", "Management", "Reception"] },
  { label: "Üyelikler", href: "/memberships", icon: WalletCards, roles: ["Management", "Reception"] },
  { label: "Ödemeler", href: "/payments", icon: Banknote, roles: ["Management", "Reception"] },
  { label: "Borç Bakiyeleri", href: "/balances", icon: ReceiptText, roles: ["Management", "Reception"] },
  { label: "Raporlar", href: "/reports", icon: ShieldCheck, roles: ["Admin", "Management"] },
  { label: "Eğitmenler", href: "/instructors", icon: UserCog, roles: ["Management"] },
];
const instructorNav: NavItem[] = [
  { label: "Bugün", href: "/dashboard", icon: Home },
  { label: "Sınıflarım", href: "/my-classes", icon: GraduationCap },
  { label: "Yoklama", href: "/attendance", icon: ClipboardCheck },
];
const systemNav: NavItem[] = [
  { label: "Kullanıcılar", href: "/users", icon: Users, roles: ["Admin"] },
  { label: "Ayarlar", href: "/settings", icon: Settings, roles: ["Admin"] },
];

function Brand() {
  return <Link href="/dashboard" className="flex items-center gap-3 px-2"><div className="grid size-9 place-items-center rounded-lg bg-[#20241f] font-black text-white">B</div><div><p className="text-sm font-bold tracking-[0.18em]">BORDER</p><p className="text-[10px] text-zinc-400">STUDIO MANAGEMENT</p></div></Link>;
}

function Navigation({ items, close }: { items: NavItem[]; close?: () => void }) {
  const path = usePathname();
  return <nav className="space-y-1">{items.map((item) => {
    const active = path === item.href || (item.href !== "/dashboard" && path.startsWith(`${item.href}/`));
    const Icon = item.icon;
    return <Link key={item.href} href={item.href} onClick={close} className={`flex h-10 items-center gap-3 rounded-lg px-3 text-sm font-medium transition-colors ${active ? "bg-[#e9ede4] text-[#304126]" : "text-zinc-600 hover:bg-zinc-100 hover:text-zinc-950"}`}><Icon size={18} strokeWidth={1.8} />{item.label}{active && <ChevronRight className="ml-auto" size={15} />}</Link>;
  })}</nav>;
}

export function AppShell({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { data: user, isLoading, isError } = useCurrentUser();
  const [mobileOpen, setMobileOpen] = useState(false);
  useEffect(() => { if (isError) router.replace("/login"); }, [isError, router]);
  const logout = useMutation({ mutationFn: authApi.logout, onSettled: () => { queryClient.clear(); router.replace("/login"); } });
  const items = useMemo(() => {
    if (!user) return [];
    const isInstructorOnly = user.roles.includes("Instructor") && !user.roles.some((r) => ["Management", "Reception", "Admin"].includes(r));
    if (isInstructorOnly) return instructorNav;
    const isAdmin = user.roles.includes("Admin");
    return managementNav.filter((item) => isAdmin || !item.roles || item.roles.some((role) => user.roles.includes(role)));
  }, [user]);
  const adminItems = systemNav.filter((item) => item.roles?.some((role) => user?.roles.includes(role)));

  if (isLoading || !user) return <div className="grid min-h-screen place-items-center"><div className="size-8 animate-spin rounded-full border-2 border-zinc-200 border-t-[#4f6240]" /><span className="sr-only">Yükleniyor</span></div>;

  const sidebar = <div className="flex h-full flex-col">
    <div className="px-4 py-6"><Brand /></div>
    <Separator />
    <div className="flex-1 overflow-y-auto px-3 py-5">
      <Navigation items={items} close={() => setMobileOpen(false)} />
      {adminItems.length > 0 && <><p className="mb-2 mt-7 px-3 text-[10px] font-semibold uppercase tracking-[0.16em] text-zinc-400">Yönetim</p><Navigation items={adminItems} close={() => setMobileOpen(false)} /></>}
    </div>
    <div className="border-t p-3">
      <div className="flex items-center gap-3 rounded-lg p-2">
        <Avatar className="size-9"><AvatarFallback className="bg-[#e9ede4] text-sm font-semibold text-[#405133]">{user.displayName.slice(0, 2).toUpperCase()}</AvatarFallback></Avatar>
        <div className="min-w-0 flex-1"><p className="truncate text-sm font-medium">{user.displayName}</p><p className="truncate text-xs text-zinc-400">{user.roles.join(" · ")}</p></div>
        <Button variant="ghost" size="icon-sm" aria-label="Çıkış yap" disabled={logout.isPending} onClick={() => logout.mutate()}><LogOut /></Button>
      </div>
    </div>
  </div>;

  return <div className="min-h-screen lg:grid lg:grid-cols-[252px_minmax(0,1fr)]">
    <aside className="fixed inset-y-0 left-0 z-30 hidden w-[252px] border-r bg-white lg:block">{sidebar}</aside>
    <div className="lg:col-start-2">
      <header className="sticky top-0 z-20 flex h-16 items-center justify-between border-b bg-white/95 px-4 backdrop-blur sm:px-7 lg:px-9">
        <div className="flex items-center gap-3 lg:hidden">
          <Sheet open={mobileOpen} onOpenChange={setMobileOpen}><SheetTrigger render={<Button variant="ghost" size="icon" aria-label="Menüyü aç" />}><Menu /></SheetTrigger><SheetContent side="left" className="w-[286px] p-0"><SheetTitle className="sr-only">Ana menü</SheetTitle>{sidebar}</SheetContent></Sheet>
          <Brand />
        </div>
        <GlobalSearch />
        <div className="flex items-center gap-3"><div className="hidden text-right sm:block"><p className="text-sm font-medium">{user.displayName}</p><p className="text-xs text-zinc-400">{user.roles[0]}</p></div><Avatar className="size-9"><AvatarFallback className="bg-[#20241f] text-xs text-white">{user.displayName.slice(0, 2).toUpperCase()}</AvatarFallback></Avatar></div>
      </header>
      <main className="p-4 sm:p-7 lg:p-9">{children}</main>
    </div>
  </div>;
}
