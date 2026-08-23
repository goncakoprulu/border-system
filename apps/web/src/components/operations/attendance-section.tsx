"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Check, ChevronRight, CircleAlert, ClipboardCheck, LoaderCircle, RotateCcw, Search, StickyNote, X } from "lucide-react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { useMemo, useRef, useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { classesApi, classKeys } from "@/lib/classes";
import { formErrorMessage } from "@/lib/form-errors";
import { AttendanceDetail, AttendanceStatus, operationKeys, operationsApi, Session } from "@/lib/operations";

const statuses: { value: AttendanceStatus; label: string; selected: string }[] = [
  { value: "Present", label: "Geldi", selected: "border-emerald-600 bg-emerald-50 text-emerald-800" },
  { value: "Absent", label: "Gelmedi", selected: "border-rose-600 bg-rose-50 text-rose-800" },
  { value: "Excused", label: "Mazeretli", selected: "border-amber-600 bg-amber-50 text-amber-800" },
  { value: "Late", label: "Geç", selected: "border-sky-600 bg-sky-50 text-sky-800" },
  { value: "MakeUp", label: "Telafi", selected: "border-violet-600 bg-violet-50 text-violet-800" },
];

const today = () => new Intl.DateTimeFormat("en-CA", { timeZone: "Europe/Istanbul" }).format(new Date());
const time = (value: string) => new Intl.DateTimeFormat("tr-TR", { timeZone: "Europe/Istanbul", hour: "2-digit", minute: "2-digit" }).format(new Date(value));

function selectedStatus(detail: AttendanceDetail, draft: Record<string, AttendanceStatus | null>, studentId: string) {
  if (Object.hasOwn(draft, studentId)) return draft[studentId];
  return detail.students.find((student) => student.studentId === studentId)?.status ?? null;
}

function sessionState(session: Session) {
  if (session.isCompleted) return { label: "Tamamlandı", className: "bg-emerald-50 text-emerald-700" };
  const now = Date.now();
  if (session.recordedCount > 0 || (new Date(session.scheduledStart).getTime() <= now && now <= new Date(session.scheduledEnd).getTime())) {
    return { label: "Devam ediyor", className: "bg-amber-50 text-amber-700" };
  }
  return { label: "Henüz alınmadı", className: "bg-zinc-100 text-zinc-600" };
}

export function AttendanceSection() {
  const searchParams = useSearchParams();
  const studentId = searchParams.get("studentId") ?? "";
  const queryClient = useQueryClient();
  const saveLock = useRef(false);
  const [selectedId, setSelectedId] = useState(() => searchParams.get("sessionId") ?? "");
  const [selectedDate, setSelectedDate] = useState(today());
  const [instructorId, setInstructorId] = useState("");
  const [classId, setClassId] = useState("");
  const [roomId, setRoomId] = useState("");
  const [draft, setDraft] = useState<Record<string, AttendanceStatus | null>>({});
  const [noteDraft, setNoteDraft] = useState<Record<string, string>>({});
  const [openNotes, setOpenNotes] = useState<Set<string>>(new Set());
  const [saveError, setSaveError] = useState("");

  const params = useMemo(() => {
    const query = new URLSearchParams({ date: selectedDate });
    if (instructorId) query.set("instructorId", instructorId);
    if (classId) query.set("classId", classId);
    if (roomId) query.set("roomId", roomId);
    if (studentId) query.set("studentId", studentId);
    return query.toString();
  }, [classId, instructorId, roomId, selectedDate, studentId]);

  const sessions = useQuery({ queryKey: operationKeys.section("sessions", params), queryFn: () => operationsApi.sessions(params) });
  const detail = useQuery({ queryKey: operationKeys.section("attendance", selectedId), queryFn: () => operationsApi.attendance(selectedId), enabled: !!selectedId });
  const rooms = useQuery({ queryKey: classKeys.rooms, queryFn: classesApi.rooms });
  const instructors = useQuery({ queryKey: classKeys.instructors, queryFn: classesApi.instructors });
  const classes = useQuery({ queryKey: classKeys.list("attendance-options"), queryFn: () => classesApi.list(new URLSearchParams({ pageSize: "100", status: "Active" })) });

  const save = useMutation({
    mutationFn: () => {
      if (!detail.data) throw new Error("Yoklama bilgileri henüz yüklenmedi.");
      const entries = detail.data.students.map((student) => ({
        studentId: student.studentId,
        status: selectedStatus(detail.data!, draft, student.studentId)!,
        notes: (Object.hasOwn(noteDraft, student.studentId) ? noteDraft[student.studentId] : student.notes)?.trim() || null,
      }));
      return operationsApi.saveAttendance(selectedId, entries);
    },
    onSuccess: async (result) => {
      queryClient.setQueryData(operationKeys.section("attendance", selectedId), result);
      setDraft({}); setNoteDraft({}); setSaveError("");
      await queryClient.invalidateQueries({ queryKey: ["operations", "sessions"] });
      const counts = Object.fromEntries(statuses.map(({ value }) => [value, result.students.filter((student) => student.status === value).length]));
      toast.success("Yoklama başarıyla kaydedildi.", { description: `${result.students.length} öğrenci · ${counts.Present} geldi · ${counts.Absent} gelmedi · ${counts.Excused} mazeretli` });
    },
    onError: (error) => {
      const message = formErrorMessage(error, "Yoklama kaydedilirken beklenmeyen bir hata oluştu.");
      setSaveError(message); toast.error(message);
    },
    onSettled: () => { saveLock.current = false; },
  });

  const changeSelection = (id: string) => { setSelectedId(id); setDraft({}); setNoteDraft({}); setOpenNotes(new Set()); setSaveError(""); };

  if (selectedId) {
    if (detail.isLoading) return <Panel><Loading /></Panel>;
    if (detail.isError) return <Panel><ErrorState message={detail.error.message} retry={() => detail.refetch()} /></Panel>;
    if (!detail.data) return null;
    const data = detail.data;
    const complete = data.students.every((student) => selectedStatus(data, draft, student.studentId));
    const current = Object.fromEntries(statuses.map(({ value }) => [value, data.students.filter((student) => selectedStatus(data, draft, student.studentId) === value).length]));

    return <Panel>
      <div className="border-b p-4 sm:p-5">
        <Button type="button" variant="ghost" className="-ml-2" disabled={save.isPending} onClick={() => changeSelection("")}>← Derslere dön</Button>
        <div className="mt-3 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
          <div><h2 className="text-lg font-semibold">{data.session.className}</h2><p className="mt-1 text-sm text-zinc-500">{time(data.session.scheduledStart)}–{time(data.session.scheduledEnd)} · {data.session.instructorName} · {data.session.roomName}</p></div>
          <Button type="button" variant="outline" disabled={save.isPending || data.students.length === 0} onClick={() => setDraft(Object.fromEntries(data.students.map((student) => [student.studentId, "Present"]))) }><Check />Tümünü Geldi Yap</Button>
        </div>
        <div className="mt-4 flex flex-wrap gap-2 text-xs"><Summary label="Öğrenci" value={data.students.length}/><Summary label="Geldi" value={current.Present}/><Summary label="Gelmedi" value={current.Absent}/><Summary label="Mazeretli" value={current.Excused}/><Summary label="Geç" value={current.Late}/><Summary label="Telafi" value={current.MakeUp}/></div>
      </div>
      {saveError && <div role="alert" className="m-4 flex gap-2 rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700"><CircleAlert className="mt-0.5 size-4 shrink-0" />{saveError}</div>}
      {data.students.length === 0 ? <Empty title="Bu sınıfta aktif öğrenci yok" detail="Ders oturumu tamamlanabilir; yoklama listesine yalnızca ders tarihinde aktif kaydı olan öğrenciler alınır."/> : <div className="divide-y">{data.students.map((student) => {
        const value = selectedStatus(data, draft, student.studentId);
        const note = Object.hasOwn(noteDraft, student.studentId) ? noteDraft[student.studentId] : student.notes ?? "";
        const noteOpen = openNotes.has(student.studentId);
        return <div key={student.studentId} className="p-4 sm:p-5">
          <div className="flex items-start justify-between gap-3"><div className="min-w-0"><p className="font-medium">{student.studentName}</p><div className="mt-1 flex flex-wrap gap-2 text-xs">{student.recentSessionCount >= 3 && student.recentAbsenceCount >= 2 && <span className="rounded-full bg-rose-50 px-2 py-0.5 font-medium text-rose-700">Son {student.recentSessionCount} derste {student.recentAbsenceCount} devamsızlık</span>}{student.studentNotes && <span className="max-w-md truncate text-zinc-500" title={student.studentNotes}>Öğrenci notu: {student.studentNotes}</span>}</div></div><Button type="button" size="sm" variant="ghost" onClick={() => setOpenNotes((currentSet) => { const next = new Set(currentSet); if (next.has(student.studentId)) next.delete(student.studentId); else next.add(student.studentId); return next; })}><StickyNote />{note ? "Notu düzenle" : "Not"}</Button></div>
          <div className="mt-3 grid grid-cols-2 gap-2 sm:grid-cols-5">{statuses.map((status) => <button type="button" key={status.value} disabled={save.isPending} aria-pressed={value === status.value} onClick={() => setDraft((currentDraft) => ({ ...currentDraft, [student.studentId]: status.value }))} className={`min-h-12 rounded-lg border px-3 text-sm font-medium transition ${value === status.value ? status.selected : "bg-white text-zinc-600 hover:bg-zinc-50"}`}>{status.label}{value === status.value && <Check className="ml-1 inline size-4" />}</button>)}</div>
          {noteOpen && <div className="mt-3"><Label htmlFor={`note-${student.studentId}`} className="sr-only">{student.studentName} için yoklama notu</Label><Textarea id={`note-${student.studentId}`} maxLength={1000} rows={2} value={note} placeholder="Örn. 15 dk geç geldi" onChange={(event) => setNoteDraft((currentDraft) => ({ ...currentDraft, [student.studentId]: event.target.value }))}/><p className="mt-1 text-right text-xs text-zinc-400">{note.length}/1000</p></div>}
        </div>;
      })}</div>}
      <div className="sticky bottom-0 z-10 flex flex-col gap-3 border-t bg-white/95 p-4 backdrop-blur sm:flex-row sm:items-center sm:justify-between">
        <div className="flex gap-1"><Button type="button" size="sm" variant="ghost" disabled={save.isPending} onClick={() => setDraft(Object.fromEntries(data.students.map((student) => [student.studentId, null])))}><X />Seçimleri temizle</Button><Button type="button" size="sm" variant="ghost" disabled={save.isPending} onClick={() => { setDraft({}); setNoteDraft({}); setSaveError(""); }}><RotateCcw />Son kaydı geri yükle</Button></div>
        <Button type="button" className="h-11 sm:min-w-44" disabled={save.isPending || !complete} aria-busy={save.isPending} onClick={() => { if (saveLock.current || save.isPending) return; saveLock.current = true; save.mutate(); }}>{save.isPending ? <><LoaderCircle className="animate-spin" />Kaydediliyor...</> : "Yoklamayı Kaydet"}</Button>
      </div>
    </Panel>;
  }

  return <Panel>
    {studentId && <div className="flex items-center justify-between gap-3 border-b bg-[#f5f7f2] px-4 py-3 text-sm text-[#526743]"><span>Öğrenci filtresi aktif</span><Button size="sm" variant="ghost" render={<Link href="/attendance" />}><X />Filtreyi kaldır</Button></div>}
    <div className="grid gap-3 border-b p-4 sm:grid-cols-2 lg:grid-cols-4">
      <Field label="Tarih"><Input type="date" value={selectedDate} onChange={(event) => { setSelectedDate(event.target.value); changeSelection(""); }}/></Field>
      <Filter label="Eğitmen" value={instructorId} onChange={setInstructorId} options={(instructors.data ?? []).map((item) => ({ value: item.id, label: item.fullName }))}/>
      <Filter label="Sınıf" value={classId} onChange={setClassId} options={(classes.data?.items ?? []).map((item) => ({ value: item.id, label: item.name }))}/>
      <Filter label="Stüdyo" value={roomId} onChange={setRoomId} options={(rooms.data ?? []).map((item) => ({ value: item.id, label: item.name }))}/>
    </div>
    {(instructorId || classId || roomId) && <div className="flex justify-end border-b px-4 py-2"><Button type="button" size="sm" variant="ghost" onClick={() => { setInstructorId(""); setClassId(""); setRoomId(""); }}><X />Filtreleri temizle</Button></div>}
    {sessions.isLoading ? <Loading/> : sessions.isError ? <ErrorState message={sessions.error.message} retry={() => sessions.refetch()}/> : sessions.data?.length === 0 ? <Empty title="Bu tarihte ders yok" detail="Seçili tarih ve filtrelere uyan aktif haftalık program bulunamadı."/> : <div className="divide-y">{sessions.data?.map((session) => { const state = sessionState(session); return <button type="button" key={session.id} onClick={() => changeSelection(session.id)} className="flex w-full items-center gap-3 p-4 text-left transition hover:bg-zinc-50 sm:gap-5 sm:p-5"><div className="w-14 shrink-0 text-base font-semibold">{time(session.scheduledStart)}</div><div className="min-w-0 flex-1"><p className="font-medium">{session.className}</p><p className="mt-1 truncate text-sm text-zinc-500">{session.instructorName} · {session.roomName} · {session.studentCount} öğrenci</p></div><span className={`hidden rounded-full px-2.5 py-1 text-xs font-medium sm:inline-flex ${state.className}`}>{state.label}</span><ChevronRight className="size-5 shrink-0 text-zinc-400" /></button>; })}</div>}
  </Panel>;
}

function Panel({ children }: { children: React.ReactNode }) { return <section className="overflow-hidden rounded-xl border bg-white shadow-sm">{children}</section>; }
function Field({ label, children }: { label: string; children: React.ReactNode }) { return <div><Label className="mb-2 block">{label}</Label>{children}</div>; }
function Filter({ label, value, onChange, options }: { label: string; value: string; onChange: (value: string) => void; options: { value: string; label: string }[] }) { return <Field label={label}><select aria-label={label} className="control" value={value} onChange={(event) => onChange(event.target.value)}><option value="">Tümü</option>{options.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}</select></Field>; }
function Summary({ label, value }: { label: string; value: number }) { return <span className="rounded-full bg-zinc-100 px-2.5 py-1 text-zinc-700"><b>{value}</b> {label.toLocaleLowerCase("tr-TR")}</span>; }
function Loading() { return <div className="space-y-3 p-5">{Array.from({ length: 5 }).map((_, index) => <div key={index} className="h-16 animate-pulse rounded-lg bg-zinc-100"/>)}</div>; }
function Empty({ title, detail }: { title: string; detail: string }) { return <div className="grid min-h-64 place-items-center p-8 text-center"><div><div className="mx-auto mb-4 grid size-12 place-items-center rounded-full bg-zinc-100 text-zinc-400"><ClipboardCheck/></div><h2 className="font-medium">{title}</h2><p className="mt-2 max-w-md text-sm text-zinc-500">{detail}</p></div></div>; }
function ErrorState({ message, retry }: { message: string; retry: () => void }) { return <div className="grid min-h-64 place-items-center p-8 text-center"><div><CircleAlert className="mx-auto mb-3 text-red-500"/><h2 className="font-medium">Veriler yüklenemedi</h2><p className="mt-2 text-sm text-zinc-500">{message}</p><Button type="button" className="mt-4" variant="outline" onClick={retry}><Search/>Tekrar dene</Button></div></div>; }
