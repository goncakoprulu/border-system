"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Archive, ArrowLeft, CalendarDays, Edit3, GraduationCap, Mail, Phone, Plus, ShieldAlert, Trash2, UserRound, Users } from "lucide-react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";
import { toast } from "sonner";
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from "@/components/ui/alert-dialog";
import { Button } from "@/components/ui/button";
import { GuardianFormDialog } from "@/components/students/guardian-form-dialog";
import { StudentFormDialog } from "@/components/students/student-form-dialog";
import { StatusBadge } from "@/components/students/status-badge";
import { useCurrentUser } from "@/hooks/use-current-user";
import { calculateAge, formatDate, Guardian, studentKeys, studentStatuses, studentStatusLabels, studentsApi } from "@/lib/students";
import { enrollmentStatusLabels } from "@/lib/classes";
import { classDetailHref, isGuid } from "@/lib/routes";
import { scheduleDayTimeText } from "@/lib/schedule-days";

export function StudentDetailClient() {
  const id = useSearchParams().get("id");
  if (!isGuid(id)) return <InvalidStudentId />;
  return <StudentDetailContent id={id} />;
}

function StudentDetailContent({ id }: { id: string }) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { data: user } = useCurrentUser();
  const canArchive = user?.roles.some((role) => role === "Admin" || role === "Management") ?? false;
  const query = useQuery({ queryKey: studentKeys.detail(id), queryFn: () => studentsApi.detail(id, canArchive), enabled: !!user });
  const [editOpen, setEditOpen] = useState(false);
  const [guardianOpen, setGuardianOpen] = useState(false);
  const [editingGuardian, setEditingGuardian] = useState<Guardian>();
  const [deletingGuardian, setDeletingGuardian] = useState<Guardian>();
  const [archiveOpen, setArchiveOpen] = useState(false);
  const statusMutation = useMutation({ mutationFn: (status: (typeof studentStatuses)[number]) => studentsApi.changeStatus(id, status), onSuccess: (student) => { queryClient.setQueryData(studentKeys.detail(id), student); queryClient.invalidateQueries({ queryKey: studentKeys.all }); toast.success("Öğrenci durumu güncellendi."); }, onError: (error) => toast.error(error.message) });
  const deleteGuardian = useMutation({ mutationFn: (guardianId: string) => studentsApi.deleteGuardian(id, guardianId), onSuccess: async () => { await queryClient.invalidateQueries({ queryKey: studentKeys.detail(id) }); setDeletingGuardian(undefined); toast.success("Veli kaydı kaldırıldı."); }, onError: (error) => toast.error(error.message) });
  const archive = useMutation({ mutationFn: () => studentsApi.archive(id), onSuccess: async () => { await queryClient.invalidateQueries({ queryKey: studentKeys.all }); toast.success("Öğrenci arşivlendi."); router.replace("/students"); }, onError: (error) => toast.error(error.message) });

  if (query.isLoading) return <div className="mx-auto max-w-6xl space-y-5"><div className="h-28 animate-pulse rounded-xl bg-zinc-200" /><div className="h-80 animate-pulse rounded-xl bg-zinc-100" /></div>;
  if (query.isError) return <div className="mx-auto grid min-h-[60vh] max-w-3xl place-items-center text-center"><div><ShieldAlert className="mx-auto mb-4 text-red-500" /><h1 className="text-xl font-semibold">Öğrenci bilgileri açılamadı</h1><p className="mt-2 text-sm text-zinc-500">Kayıt bulunamadı veya erişim yetkiniz yok.</p><Button className="mt-5" variant="outline" render={<Link href="/students" />}>Öğrencilere dön</Button></div></div>;
  const student = query.data!;
  const age = calculateAge(student.birthDate);

  return <div className="mx-auto max-w-6xl">
    <Link href="/students" className="mb-5 inline-flex items-center gap-2 text-sm text-zinc-500 hover:text-zinc-900"><ArrowLeft size={16} />Öğrencilere dön</Link>
    <header className="flex flex-col gap-5 border-b pb-7 sm:flex-row sm:items-start sm:justify-between">
      <div className="flex items-start gap-4"><div className="grid size-14 shrink-0 place-items-center rounded-2xl bg-[#e8eee2] text-lg font-semibold text-[#526743]">{student.firstName[0]}{student.lastName[0]}</div><div><div className="flex flex-wrap items-center gap-3"><h1 className="text-2xl font-semibold tracking-tight sm:text-3xl">{student.firstName} {student.lastName}</h1><StatusBadge status={student.status} /></div><div className="mt-2 flex flex-wrap gap-x-5 gap-y-1 text-sm text-zinc-500">{student.phone && <a href={`tel:${student.phone}`} className="inline-flex items-center gap-1.5 hover:text-zinc-900"><Phone size={14} />{student.phone}</a>}{student.email && <a href={`mailto:${student.email}`} className="inline-flex items-center gap-1.5 hover:text-zinc-900"><Mail size={14} />{student.email}</a>}</div></div></div>
      <div className="flex flex-wrap gap-2"><Button variant="outline" onClick={() => setEditOpen(true)}><Edit3 />Düzenle</Button>{canArchive && <Button variant="outline" className="text-amber-700" onClick={() => setArchiveOpen(true)}><Archive />Arşivle</Button>}</div>
    </header>

    <div className="grid gap-6 py-7 lg:grid-cols-[minmax(0,1.6fr)_minmax(280px,.8fr)]">
      <div className="space-y-6">
        <Section title="Genel bilgiler" icon={<UserRound size={18} />}>
          <dl className="grid gap-x-8 gap-y-5 sm:grid-cols-2"><Info label="Ad soyad" value={`${student.firstName} ${student.lastName}`} /><Info label="Kayıt tarihi" value={formatDate(student.registrationDate)} /><Info label="Doğum tarihi" value={student.birthDate ? `${formatDate(student.birthDate)}${age !== null ? ` · ${age} yaş` : ""}` : "—"} /><Info label="Cinsiyet" value={student.gender ?? "—"} /><Info label="Telefon" value={student.phone ?? "—"} /><Info label="E-posta" value={student.email ?? "—"} /></dl>
          <div className="mt-6 border-t pt-5"><p className="text-xs font-medium uppercase tracking-wide text-zinc-400">Notlar</p><p className="mt-2 whitespace-pre-wrap text-sm leading-6 text-zinc-700">{student.notes || "Bu öğrenci için not eklenmemiş."}</p></div>
        </Section>

        <Section title="Veliler ve yakınlar" icon={<Users size={18} />} action={<Button size="sm" variant="outline" onClick={() => { setEditingGuardian(undefined); setGuardianOpen(true); }}><Plus />Veli ekle</Button>}>
          {student.guardians.length === 0 ? <Empty text="Henüz veli veya yakın bilgisi eklenmemiş." /> : <div className="divide-y">{student.guardians.map((guardian) => <div key={guardian.id} className="flex flex-col gap-3 py-4 first:pt-0 last:pb-0 sm:flex-row sm:items-center"><div className="min-w-0 flex-1"><div className="flex items-center gap-2"><p className="font-medium">{guardian.firstName} {guardian.lastName}</p><span className="rounded-md bg-zinc-100 px-2 py-0.5 text-xs text-zinc-600">{guardian.relationship}</span></div><div className="mt-1 flex flex-wrap gap-x-4 text-sm text-zinc-500">{guardian.phone && <a href={`tel:${guardian.phone}`}>{guardian.phone}</a>}{guardian.email && <a href={`mailto:${guardian.email}`} className="hover:underline">{guardian.email}</a>}</div></div><div className="flex gap-1"><Button size="sm" variant="ghost" onClick={() => { setEditingGuardian(guardian); setGuardianOpen(true); }}>Düzenle</Button><Button size="icon-sm" variant="ghost" className="text-red-600" aria-label={`${guardian.firstName} veli kaydını kaldır`} onClick={() => setDeletingGuardian(guardian)}><Trash2 /></Button></div></div>)}</div>}
        </Section>
      </div>

      <aside className="space-y-6">
        <Section title="Öğrenci durumu" icon={<CalendarDays size={18} />}>
          <label htmlFor="student-status" className="mb-2 block text-xs font-medium uppercase tracking-wide text-zinc-400">Operasyonel durum</label><select id="student-status" value={student.status} disabled={statusMutation.isPending} onChange={(event) => statusMutation.mutate(event.target.value as (typeof studentStatuses)[number])} className="h-10 w-full rounded-lg border bg-white px-3 text-sm">{studentStatuses.map((status) => <option key={status} value={status}>{studentStatusLabels[status]}</option>)}</select><p className="mt-3 text-xs leading-5 text-zinc-500">Durum değişikliği öğrenciyi veya geçmiş kayıtlarını silmez.</p>
        </Section>
        <Section title="Sınıflar" icon={<GraduationCap size={18} />}>{student.classEnrollments.length === 0 ? <Empty text="Henüz sınıf kaydı bulunmuyor." compact /> : <div className="space-y-3">{student.classEnrollments.map((enrollment) => <Link key={enrollment.id} href={classDetailHref(enrollment.classId)} className="block rounded-lg border p-3 hover:border-[#879878]"><div className="flex items-start justify-between gap-2"><div><p className="text-sm font-medium">{enrollment.className}</p><p className="mt-1 text-xs text-zinc-500">{enrollment.instructorName} · {enrollment.roomName}</p></div><span className="text-xs text-zinc-500">{enrollmentStatusLabels[enrollment.status]}</span></div><p className="mt-2 text-xs text-zinc-500">{enrollment.schedules.map((x) => scheduleDayTimeText(x.dayOfWeek, x.startTime, x.endTime)).join(", ") || "Program yok"}</p><p className="mt-1 text-xs text-zinc-400">{formatDate(enrollment.startDate)} – {formatDate(enrollment.endDate)}</p></Link>)}</div>}</Section>
        <Section title="Yoklama" icon={<CalendarDays size={18} />}><Empty text="Yoklama geçmişi henüz bu ekrana bağlı değil." compact /></Section>
        <Section title="Üyelik ve ödemeler" icon={<Archive size={18} />}><Empty text="Finansal işlemler bu fazda uygulanmadı." compact /></Section>
      </aside>
    </div>

    <StudentFormDialog open={editOpen} onOpenChange={setEditOpen} student={student} />
    <GuardianFormDialog studentId={id} guardian={editingGuardian} open={guardianOpen} onOpenChange={setGuardianOpen} />
    <AlertDialog open={!!deletingGuardian} onOpenChange={(open) => !open && setDeletingGuardian(undefined)}><AlertDialogContent><AlertDialogHeader><AlertDialogTitle>Veli kaydı kaldırılsın mı?</AlertDialogTitle><AlertDialogDescription>{deletingGuardian?.firstName} {deletingGuardian?.lastName} öğrenciyle ilişkilendirilen veli listesinden kaldırılacak.</AlertDialogDescription></AlertDialogHeader><AlertDialogFooter><AlertDialogCancel>Vazgeç</AlertDialogCancel><AlertDialogAction variant="destructive" disabled={deleteGuardian.isPending} onClick={() => deletingGuardian && deleteGuardian.mutate(deletingGuardian.id)}>Kaydı kaldır</AlertDialogAction></AlertDialogFooter></AlertDialogContent></AlertDialog>
    <AlertDialog open={archiveOpen} onOpenChange={setArchiveOpen}><AlertDialogContent><AlertDialogHeader><AlertDialogTitle>Öğrenci arşivlensin mi?</AlertDialogTitle><AlertDialogDescription>{student.firstName} {student.lastName} normal öğrenci listesinden kaldırılacak. Tarihsel kayıtlar korunacaktır.</AlertDialogDescription></AlertDialogHeader><AlertDialogFooter><AlertDialogCancel>Vazgeç</AlertDialogCancel><AlertDialogAction className="bg-amber-700 hover:bg-amber-800" disabled={archive.isPending} onClick={() => archive.mutate()}>Arşivle</AlertDialogAction></AlertDialogFooter></AlertDialogContent></AlertDialog>
  </div>;
}

function InvalidStudentId() {
  return <div className="mx-auto grid min-h-[60vh] max-w-3xl place-items-center text-center"><div><ShieldAlert className="mx-auto mb-4 text-amber-600" /><h1 className="text-xl font-semibold">Geçersiz öğrenci bağlantısı</h1><p className="mt-2 text-sm text-zinc-500">Öğrenci detayını açmak için geçerli bir kayıt bağlantısı kullanın.</p><Button className="mt-5" variant="outline" render={<Link href="/students/" />}>Öğrencilere dön</Button></div></div>;
}

function Section({ title, icon, action, children }: { title: string; icon: React.ReactNode; action?: React.ReactNode; children: React.ReactNode }) { return <section className="rounded-xl border bg-white p-5 shadow-sm sm:p-6"><div className="mb-5 flex items-center justify-between gap-3"><h2 className="flex items-center gap-2 text-base font-semibold">{icon}{title}</h2>{action}</div>{children}</section>; }
function Info({ label, value }: { label: string; value: string }) { return <div><dt className="text-xs font-medium uppercase tracking-wide text-zinc-400">{label}</dt><dd className="mt-1.5 text-sm font-medium text-zinc-800">{value}</dd></div>; }
function Empty({ text, compact = false }: { text: string; compact?: boolean }) { return <div className={`rounded-lg border border-dashed bg-zinc-50/70 px-4 text-center text-sm leading-6 text-zinc-500 ${compact ? "py-5" : "py-8"}`}>{text}</div>; }
