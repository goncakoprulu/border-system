"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Archive, ArrowLeft, CalendarDays, Edit3, MapPin, Plus, ShieldAlert, Users } from "lucide-react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";
import { toast } from "sonner";
import { ClassFormDialog } from "@/components/classes/class-form-dialog";
import { ClassStatusBadge } from "@/components/classes/class-status-badge";
import { EnrollmentDialog } from "@/components/classes/enrollment-dialog";
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from "@/components/ui/alert-dialog";
import { Button } from "@/components/ui/button";
import { useCurrentUser } from "@/hooks/use-current-user";
import { classesApi, classKeys, classStatuses, classStatusLabels, displayTime, enrollmentStatusLabels } from "@/lib/classes";
import { scheduleDayLabel } from "@/lib/schedule-days";
import { formatDate } from "@/lib/students";
import { isGuid } from "@/lib/routes";

export function ClassDetailClient() {
  const id = useSearchParams().get("id");
  if (!isGuid(id)) return <InvalidClassId />;
  return <ClassDetailContent id={id} />;
}

function ClassDetailContent({ id }: { id: string }) {
  const router = useRouter(); const queryClient = useQueryClient(); const { data: user } = useCurrentUser();
  const canManage = user?.roles.some((x) => ["Admin", "Management", "Reception"].includes(x)) ?? false; const canArchive = user?.roles.some((x) => x === "Admin" || x === "Management") ?? false;
  const query = useQuery({ queryKey: classKeys.detail(id), queryFn: () => classesApi.detail(id), enabled: !!user }); const [editOpen, setEditOpen] = useState(false); const [enrollOpen, setEnrollOpen] = useState(false); const [ending, setEnding] = useState<{ id: string; name: string }>(); const [archiveOpen, setArchiveOpen] = useState(false);
  const status = useMutation({ mutationFn: (value: (typeof classStatuses)[number]) => classesApi.changeStatus(id, value), onSuccess: (data) => { queryClient.setQueryData(classKeys.detail(id), data); queryClient.invalidateQueries({ queryKey: classKeys.all }); toast.success("Sınıf durumu güncellendi."); }, onError: (e) => toast.error(e.message) });
  const end = useMutation({ mutationFn: (enrollmentId: string) => classesApi.endEnrollment(id, enrollmentId, null), onSuccess: async () => { await queryClient.invalidateQueries({ queryKey: classKeys.detail(id) }); await queryClient.invalidateQueries({ queryKey: classKeys.all }); setEnding(undefined); toast.success("Öğrencinin sınıf kaydı sonlandırıldı."); }, onError: (e) => toast.error(e.message) });
  const archive = useMutation({ mutationFn: () => classesApi.archive(id), onSuccess: async () => { await queryClient.invalidateQueries({ queryKey: classKeys.all }); toast.success("Sınıf arşivlendi."); router.replace("/classes"); }, onError: (e) => toast.error(e.message) });
  if (query.isLoading) return <div className="mx-auto max-w-6xl"><div className="h-80 animate-pulse rounded-xl bg-zinc-100" /></div>;
  if (query.isError) return <div className="mx-auto grid min-h-[60vh] max-w-3xl place-items-center text-center"><div><ShieldAlert className="mx-auto mb-4 text-red-500" /><h1 className="text-xl font-semibold">Sınıf açılamadı</h1><p className="mt-2 text-sm text-zinc-500">Kayıt bulunamadı veya bu sınıfa erişim yetkiniz yok.</p><Button className="mt-5" variant="outline" render={<Link href={canManage ? "/classes" : "/my-classes"} />}>Listeye dön</Button></div></div>;
  const item = query.data!; const active = item.enrollments.filter((x) => x.status === "Active"); const history = item.enrollments.filter((x) => x.status !== "Active");
  return <div className="mx-auto max-w-6xl"><Link href={canManage ? "/classes" : "/my-classes"} className="mb-5 inline-flex items-center gap-2 text-sm text-zinc-500"><ArrowLeft size={16} />Sınıflara dön</Link>
    <header className="flex flex-col gap-5 border-b pb-7 sm:flex-row sm:justify-between"><div><div className="flex flex-wrap items-center gap-3"><h1 className="text-2xl font-semibold sm:text-3xl">{item.name}</h1><ClassStatusBadge status={item.status} /></div><p className="mt-2 text-sm text-zinc-500">{item.instructorName} · {item.roomName} · {active.length}/{item.capacity} öğrenci</p></div>{canManage && <div className="flex gap-2"><Button variant="outline" onClick={() => setEditOpen(true)}><Edit3 />Düzenle</Button>{canArchive && <Button variant="outline" className="text-amber-700" onClick={() => setArchiveOpen(true)}><Archive />Arşivle</Button>}</div>}</header>
    <div className="grid gap-6 py-7 lg:grid-cols-[1fr_320px]"><div className="space-y-6"><Section title="Haftalık program" icon={<CalendarDays size={18} />}>{item.schedules.length ? <div className="grid gap-3 sm:grid-cols-2">{item.schedules.map((s) => <div key={s.id} className="rounded-lg border bg-zinc-50 p-4"><p className="font-medium">{scheduleDayLabel(s.dayOfWeek)}</p><p className="mt-1 text-sm text-zinc-500">{displayTime(s.startTime)}–{displayTime(s.endTime)}</p></div>)}</div> : <Empty text="Bu sınıf için program eklenmedi." />}</Section>
      <Section title={`Aktif öğrenciler (${active.length}/${item.capacity})`} icon={<Users size={18} />} action={canManage ? <Button size="sm" variant="outline" disabled={active.length >= item.capacity} onClick={() => setEnrollOpen(true)}><Plus />Öğrenci ekle</Button> : undefined}>{active.length ? <div className="divide-y">{active.map((x) => <div key={x.id} className="flex items-center justify-between gap-3 py-3 first:pt-0 last:pb-0"><div><p className="font-medium">{x.studentName}</p><p className="text-xs text-zinc-500">{x.phone ?? "Telefon belirtilmedi"} · {formatDate(x.startDate)} tarihinden beri</p></div>{canManage && <Button size="sm" variant="ghost" className="text-amber-700" onClick={() => setEnding({ id: x.id, name: x.studentName })}>Kaydı bitir</Button>}</div>)}</div> : <Empty text="Henüz aktif öğrenci kaydı yok." />}</Section>
      <Section title="Kayıt geçmişi" icon={<Archive size={18} />}>{history.length ? <div className="divide-y">{history.map((x) => <div key={x.id} className="py-3 first:pt-0 last:pb-0"><div className="flex justify-between gap-2"><p className="font-medium">{x.studentName}</p><span className="text-xs text-zinc-500">{enrollmentStatusLabels[x.status]}</span></div><p className="mt-1 text-xs text-zinc-500">{formatDate(x.startDate)} – {formatDate(x.endDate)}</p></div>)}</div> : <Empty text="Sonlandırılmış kayıt bulunmuyor." />}</Section></div>
      <aside className="space-y-6"><Section title="Sınıf bilgileri" icon={<MapPin size={18} />}><dl className="space-y-4"><Info label="Eğitmen" value={item.instructorName} /><Info label="Stüdyo" value={item.roomName} /><Info label="Kapasite" value={`${item.capacity} kişi`} /><Info label="Seviye" value={item.level ?? "—"} /><Info label="Yaş grubu" value={item.ageGroup ?? "—"} /><Info label="Tarih" value={`${formatDate(item.startDate)} – ${formatDate(item.endDate)}`} /></dl>{item.description && <p className="mt-5 border-t pt-4 text-sm leading-6 text-zinc-600">{item.description}</p>}</Section>{canManage && <Section title="Operasyonel durum" icon={<CalendarDays size={18} />}><select value={item.status} disabled={status.isPending} onChange={(e) => status.mutate(e.target.value as (typeof classStatuses)[number])} className="h-10 w-full rounded-lg border bg-white px-3 text-sm">{classStatuses.map((x) => <option key={x} value={x}>{classStatusLabels[x]}</option>)}</select></Section>}<Section title="Yoklama" icon={<Users size={18} />}><Empty text="Ders oturumları ve yoklama Phase 4 kapsamında eklenecek." /></Section></aside></div>
    {canManage && <><ClassFormDialog open={editOpen} onOpenChange={setEditOpen} item={item} /><EnrollmentDialog open={enrollOpen} onOpenChange={setEnrollOpen} item={item} /></>}
    <AlertDialog open={!!ending} onOpenChange={(open) => !open && setEnding(undefined)}><AlertDialogContent><AlertDialogHeader><AlertDialogTitle>Sınıf kaydı sonlandırılsın mı?</AlertDialogTitle><AlertDialogDescription>{ending?.name} aktif listeden çıkarılacak; kayıt geçmişi korunacaktır.</AlertDialogDescription></AlertDialogHeader><AlertDialogFooter><AlertDialogCancel>Vazgeç</AlertDialogCancel><AlertDialogAction disabled={end.isPending} onClick={() => ending && end.mutate(ending.id)}>Kaydı sonlandır</AlertDialogAction></AlertDialogFooter></AlertDialogContent></AlertDialog>
    <AlertDialog open={archiveOpen} onOpenChange={setArchiveOpen}><AlertDialogContent><AlertDialogHeader><AlertDialogTitle>Sınıf arşivlensin mi?</AlertDialogTitle><AlertDialogDescription>Sınıf normal listeden kaldırılacak ve iptal durumuna alınacak. Öğrenci kayıt geçmişi korunacaktır.</AlertDialogDescription></AlertDialogHeader><AlertDialogFooter><AlertDialogCancel>Vazgeç</AlertDialogCancel><AlertDialogAction disabled={archive.isPending} onClick={() => archive.mutate()}>Arşivle</AlertDialogAction></AlertDialogFooter></AlertDialogContent></AlertDialog>
  </div>;
}

function InvalidClassId() {
  return <div className="mx-auto grid min-h-[60vh] max-w-3xl place-items-center text-center"><div><ShieldAlert className="mx-auto mb-4 text-amber-600" /><h1 className="text-xl font-semibold">Geçersiz sınıf bağlantısı</h1><p className="mt-2 text-sm text-zinc-500">Sınıf detayını açmak için geçerli bir kayıt bağlantısı kullanın.</p><Button className="mt-5" variant="outline" render={<Link href="/classes/" />}>Sınıflara dön</Button></div></div>;
}
function Section({ title, icon, action, children }: { title: string; icon: React.ReactNode; action?: React.ReactNode; children: React.ReactNode }) { return <section className="rounded-xl border bg-white p-5 shadow-sm"><div className="mb-5 flex items-center justify-between gap-3"><h2 className="flex items-center gap-2 font-semibold">{icon}{title}</h2>{action}</div>{children}</section>; }
function Empty({ text }: { text: string }) { return <p className="rounded-lg border border-dashed p-4 text-center text-sm text-zinc-500">{text}</p>; }
function Info({ label, value }: { label: string; value: string }) { return <div><dt className="text-xs uppercase tracking-wide text-zinc-400">{label}</dt><dd className="mt-1 text-sm font-medium">{value}</dd></div>; }
