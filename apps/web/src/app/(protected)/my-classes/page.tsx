"use client";

import { useQuery } from "@tanstack/react-query";
import { CalendarDays, ChevronRight, GraduationCap } from "lucide-react";
import Link from "next/link";
import { ClassStatusBadge } from "@/components/classes/class-status-badge";
import { classesApi, classKeys, scheduleText } from "@/lib/classes";
import { classDetailHref } from "@/lib/routes";

export default function MyClassesPage() {
  const params = new URLSearchParams({ page: "1", pageSize: "100" });
  const query = useQuery({ queryKey: classKeys.list("mine"), queryFn: () => classesApi.list(params) });
  return <div className="mx-auto max-w-5xl"><p className="text-sm font-medium text-[#61734f]">Eğitmen görünümü</p><h1 className="mt-1 text-2xl font-semibold sm:text-3xl">Sınıflarım</h1><p className="mt-2 text-sm text-zinc-500">Size atanmış sınıfların program ve öğrenci listelerine salt okunur erişim.</p>
    {query.isLoading ? <div className="mt-7 h-64 animate-pulse rounded-xl bg-zinc-100" /> : query.isError ? <State text="Sınıflarınız yüklenemedi." /> : !query.data?.items.length ? <State text="Henüz size atanmış bir sınıf bulunmuyor." /> : <div className="mt-7 grid gap-4 md:grid-cols-2">{query.data.items.map((item) => <Link key={item.id} href={classDetailHref(item.id)} className="rounded-xl border bg-white p-5 shadow-sm transition hover:border-[#879878]"><div className="flex items-start justify-between gap-3"><div className="grid size-10 place-items-center rounded-xl bg-[#edf1e9] text-[#526743]"><GraduationCap size={20} /></div><ClassStatusBadge status={item.status} /></div><h2 className="mt-4 text-lg font-semibold">{item.name}</h2><p className="mt-1 text-sm text-zinc-500">{item.roomName} · {item.activeStudentCount}/{item.capacity} öğrenci</p><p className="mt-4 flex items-start gap-2 text-sm text-zinc-600"><CalendarDays size={16} className="mt-0.5 shrink-0" />{scheduleText(item.schedules)}</p><span className="mt-5 flex items-center justify-end gap-1 text-sm font-medium text-[#526743]">Sınıfı aç <ChevronRight size={16} /></span></Link>)}</div>}
  </div>;
}
function State({ text }: { text: string }) { return <div className="mt-7 grid min-h-64 place-items-center rounded-xl border border-dashed bg-white p-8 text-center text-sm text-zinc-500">{text}</div>; }
