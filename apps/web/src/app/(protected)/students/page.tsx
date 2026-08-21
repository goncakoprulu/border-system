"use client";

import { useQuery } from "@tanstack/react-query";
import { ChevronLeft, ChevronRight, Plus, Search, UserRound, Users } from "lucide-react";
import Link from "next/link";
import { useDeferredValue, useMemo, useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { StudentFormDialog } from "@/components/students/student-form-dialog";
import { StatusBadge } from "@/components/students/status-badge";
import { useCurrentUser } from "@/hooks/use-current-user";
import { formatDate, studentKeys, studentStatuses, studentStatusLabels, studentsApi } from "@/lib/students";
import { studentDetailHref } from "@/lib/routes";

export default function StudentsPage() {
  const { data: user } = useCurrentUser();
  const [search, setSearch] = useState("");
  const deferredSearch = useDeferredValue(search);
  const [status, setStatus] = useState("");
  const [sort, setSort] = useState("name-asc");
  const [page, setPage] = useState(1);
  const [includeArchived, setIncludeArchived] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const canArchive = user?.roles.some((role) => role === "Admin" || role === "Management") ?? false;
  const params = useMemo(() => {
    const result = new URLSearchParams({ page: String(page), pageSize: "20", sortBy: sort.split("-")[0], sortDirection: sort.split("-")[1] });
    if (deferredSearch.trim()) result.set("search", deferredSearch.trim());
    if (status) result.set("status", status);
    if (includeArchived && canArchive) result.set("includeArchived", "true");
    return result;
  }, [canArchive, deferredSearch, includeArchived, page, sort, status]);
  const query = useQuery({ queryKey: studentKeys.list(params.toString()), queryFn: () => studentsApi.list(params), placeholderData: (previous) => previous });
  const resetPage = () => setPage(1);

  return <div className="mx-auto max-w-7xl">
    <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
      <div><p className="text-sm font-medium text-[#61734f]">Öğrenci yönetimi</p><h1 className="mt-1 text-2xl font-semibold tracking-tight sm:text-3xl">Öğrenciler</h1><p className="mt-2 text-sm text-zinc-500">BORDER öğrencilerini, iletişim bilgilerini ve güncel durumlarını yönetin.</p></div>
      <Button className="h-10 bg-[#20241f]" onClick={() => setCreateOpen(true)}><Plus />Yeni öğrenci</Button>
    </div>

    <section className="mt-7 overflow-hidden rounded-xl border bg-white shadow-sm">
      <div className="flex flex-col gap-3 border-b p-4 lg:flex-row lg:items-center">
        <div className="relative min-w-0 flex-1"><Search className="absolute left-3 top-1/2 -translate-y-1/2 text-zinc-400" size={17} /><Input value={search} onChange={(event) => { setSearch(event.target.value); resetPage(); }} className="h-10 pl-9" placeholder="Öğrenci ara..." aria-label="Öğrenci ara" /></div>
        <div className="grid grid-cols-2 gap-3 sm:flex">
          <select value={status} onChange={(event) => { setStatus(event.target.value); resetPage(); }} className="h-10 rounded-lg border bg-white px-3 text-sm" aria-label="Duruma göre filtrele"><option value="">Tüm durumlar</option>{studentStatuses.map((item) => <option key={item} value={item}>{studentStatusLabels[item]}</option>)}</select>
          <select value={sort} onChange={(event) => { setSort(event.target.value); resetPage(); }} className="h-10 rounded-lg border bg-white px-3 text-sm" aria-label="Sıralama"><option value="name-asc">Ada göre A–Z</option><option value="name-desc">Ada göre Z–A</option><option value="registrationDate-desc">En yeni kayıt</option><option value="registrationDate-asc">En eski kayıt</option></select>
        </div>
        {canArchive && <label className="flex h-10 items-center gap-2 whitespace-nowrap rounded-lg border px-3 text-sm text-zinc-600"><input type="checkbox" checked={includeArchived} onChange={(event) => { setIncludeArchived(event.target.checked); resetPage(); }} className="size-4 accent-[#4f6240]" />Arşivlenenler</label>}
      </div>

      {query.isLoading ? <LoadingRows /> : query.isError ? <State icon={<Users />} title="Öğrenciler yüklenemedi" detail={query.error.message} /> : query.data?.items.length === 0 ? <State icon={<UserRound />} title={search || status ? "Aramanızla eşleşen öğrenci yok" : "Henüz öğrenci kaydı yok"} detail={search || status ? "Arama veya filtreleri değiştirerek tekrar deneyin." : "İlk öğrenci kaydını oluşturmak için Yeni öğrenci düğmesini kullanın."} /> : <>
        <div className="hidden md:block"><Table><TableHeader><TableRow className="bg-zinc-50/70"><TableHead className="pl-5">Öğrenci</TableHead><TableHead>Telefon</TableHead><TableHead>E-posta</TableHead><TableHead>Durum</TableHead><TableHead>Kayıt tarihi</TableHead><TableHead className="w-16" /></TableRow></TableHeader><TableBody>{query.data?.items.map((student) => <TableRow key={student.id} className="group"><TableCell className="pl-5"><Link href={studentDetailHref(student.id)} className="flex items-center gap-3 font-medium hover:text-[#526743]"><div className="grid size-9 place-items-center rounded-full bg-[#edf1e9] text-xs font-semibold text-[#526743]">{student.firstName[0]}{student.lastName[0]}</div><div><p>{student.firstName} {student.lastName}</p>{student.isArchived && <p className="text-xs font-normal text-amber-600">Arşivlendi</p>}</div></Link></TableCell><TableCell className="text-zinc-600">{student.phone ?? "—"}</TableCell><TableCell>{student.email ? <a className="text-zinc-600 hover:underline" href={`mailto:${student.email}`}>{student.email}</a> : "—"}</TableCell><TableCell><StatusBadge status={student.status} /></TableCell><TableCell className="text-zinc-600">{formatDate(student.registrationDate)}</TableCell><TableCell><Button variant="ghost" size="icon-sm" render={<Link href={studentDetailHref(student.id)} aria-label={`${student.firstName} detayını aç`} />}><ChevronRight /></Button></TableCell></TableRow>)}</TableBody></Table></div>
        <div className="divide-y md:hidden">{query.data?.items.map((student) => <Link key={student.id} href={studentDetailHref(student.id)} className="flex min-h-28 items-start gap-3 p-4 active:bg-zinc-50"><div className="grid size-10 shrink-0 place-items-center rounded-full bg-[#edf1e9] text-sm font-semibold text-[#526743]">{student.firstName[0]}{student.lastName[0]}</div><div className="min-w-0 flex-1"><div className="flex items-start justify-between gap-2"><p className="font-medium">{student.firstName} {student.lastName}</p><StatusBadge status={student.status} /></div><p className="mt-2 text-sm text-zinc-600">{student.phone ?? "Telefon belirtilmedi"}</p><p className="truncate text-sm text-zinc-400">{student.email ?? "E-posta belirtilmedi"}</p></div><ChevronRight className="mt-9 shrink-0 text-zinc-300" size={18} /></Link>)}</div>
      </>}

      {query.data && query.data.totalCount > 0 && <div className="flex items-center justify-between border-t px-4 py-3"><p className="text-sm text-zinc-500"><span className="font-medium text-zinc-800">{query.data.totalCount}</span> öğrenci · Sayfa {query.data.page}/{Math.max(query.data.totalPages, 1)}</p><div className="flex gap-2"><Button variant="outline" size="icon-sm" aria-label="Önceki sayfa" disabled={page <= 1 || query.isFetching} onClick={() => setPage((value) => value - 1)}><ChevronLeft /></Button><Button variant="outline" size="icon-sm" aria-label="Sonraki sayfa" disabled={page >= query.data.totalPages || query.isFetching} onClick={() => setPage((value) => value + 1)}><ChevronRight /></Button></div></div>}
    </section>
    <StudentFormDialog open={createOpen} onOpenChange={setCreateOpen} />
  </div>;
}

function LoadingRows() { return <div className="space-y-3 p-5">{Array.from({ length: 6 }).map((_, index) => <div key={index} className="h-14 animate-pulse rounded-lg bg-zinc-100" />)}</div>; }
function State({ icon, title, detail }: { icon: React.ReactNode; title: string; detail: string }) { return <div className="grid min-h-80 place-items-center p-8 text-center"><div><div className="mx-auto mb-4 grid size-12 place-items-center rounded-full bg-zinc-100 text-zinc-400">{icon}</div><h2 className="font-medium">{title}</h2><p className="mx-auto mt-2 max-w-md text-sm leading-6 text-zinc-500">{detail}</p></div></div>; }
