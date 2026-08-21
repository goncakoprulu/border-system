"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Search } from "lucide-react";
import { useDeferredValue, useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { ClassDetail, classesApi, classKeys } from "@/lib/classes";
import { studentsApi } from "@/lib/students";

const today = () => new Intl.DateTimeFormat("en-CA", { timeZone: "Europe/Istanbul" }).format(new Date());
export function EnrollmentDialog({ open, onOpenChange, item }: { open: boolean; onOpenChange: (open: boolean) => void; item: ClassDetail }) {
  const queryClient = useQueryClient(); const [search, setSearch] = useState(""); const deferred = useDeferredValue(search); const [selected, setSelected] = useState(""); const [startDate, setStartDate] = useState(today());
  const params = new URLSearchParams({ search: deferred.trim(), status: "Active", page: "1", pageSize: "10" });
  const students = useQuery({ queryKey: ["enrollment-students", params.toString()], queryFn: () => studentsApi.list(params), enabled: open && deferred.trim().length >= 2 });
  const activeIds = new Set(item.enrollments.filter((x) => x.status === "Active").map((x) => x.studentId));
  const enroll = useMutation({ mutationFn: () => classesApi.enroll(item.id, selected, startDate), onSuccess: async () => { await queryClient.invalidateQueries({ queryKey: classKeys.detail(item.id) }); await queryClient.invalidateQueries({ queryKey: classKeys.all }); toast.success("Öğrenci sınıfa kaydedildi."); onOpenChange(false); }, onError: (e) => toast.error(e.message) });
  return <Dialog open={open} onOpenChange={onOpenChange}><DialogContent className="sm:max-w-xl"><DialogHeader><DialogTitle>Sınıfa öğrenci ekle</DialogTitle><DialogDescription>Öğrenciyi sunucuda arayın. Kapasite ve mükerrer kayıt kontrolü kaydetme sırasında yeniden yapılır.</DialogDescription></DialogHeader>
    <div className="relative"><Search className="absolute left-3 top-1/2 -translate-y-1/2 text-zinc-400" size={16} /><Input value={search} onChange={(e) => { setSearch(e.target.value); setSelected(""); }} className="pl-9" placeholder="Ad, telefon veya e-posta ile ara" /></div>
    <div className="max-h-56 divide-y overflow-y-auto rounded-lg border">{deferred.trim().length < 2 ? <p className="p-4 text-sm text-zinc-500">Aramak için en az 2 karakter yazın.</p> : students.isLoading ? <p className="p-4 text-sm text-zinc-500">Aranıyor…</p> : students.data?.items.length ? students.data.items.map((student) => { const disabled = activeIds.has(student.id); return <button type="button" disabled={disabled} key={student.id} onClick={() => setSelected(student.id)} className={`flex w-full justify-between p-3 text-left text-sm disabled:opacity-45 ${selected === student.id ? "bg-[#edf1e9]" : "hover:bg-zinc-50"}`}><span className="font-medium">{student.firstName} {student.lastName}</span><span className="text-zinc-500">{disabled ? "Zaten kayıtlı" : student.phone ?? student.email ?? ""}</span></button>; }) : <p className="p-4 text-sm text-zinc-500">Eşleşen aktif öğrenci bulunamadı.</p>}</div>
    <div><label className="mb-2 block text-sm font-medium" htmlFor="enroll-start">Başlangıç tarihi</label><Input id="enroll-start" type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} /></div>
    <DialogFooter><Button variant="outline" onClick={() => onOpenChange(false)}>Vazgeç</Button><Button disabled={!selected || !startDate || enroll.isPending} onClick={() => enroll.mutate()}>Kaydı tamamla</Button></DialogFooter>
  </DialogContent></Dialog>;
}
