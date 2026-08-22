"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Archive,
  ArrowLeft,
  Banknote,
  CalendarCheck,
  CalendarDays,
  Edit3,
  GraduationCap,
  Mail,
  Phone,
  Plus,
  ReceiptText,
  RefreshCw,
  ShieldAlert,
  Trash2,
  UserRound,
  Users,
  WalletCards,
} from "lucide-react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";
import { toast } from "sonner";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Button } from "@/components/ui/button";
import {
  MembershipDialog,
  PaymentDialog,
} from "@/components/operations/finance-dialogs";
import { GuardianFormDialog } from "@/components/students/guardian-form-dialog";
import { StudentClassAssignmentDialog } from "@/components/students/student-class-assignment-dialog";
import { StudentFormDialog } from "@/components/students/student-form-dialog";
import { StatusBadge } from "@/components/students/status-badge";
import { useCurrentUser } from "@/hooks/use-current-user";
import { classesApi, enrollmentStatusLabels } from "@/lib/classes";
import { formErrorMessage } from "@/lib/form-errors";
import {
  operationKeys,
  operationsApi,
  StudentAttendanceHistory,
  StudentFinanceOverview,
} from "@/lib/operations";
import { classDetailHref, isGuid } from "@/lib/routes";
import { scheduleDayTimeText } from "@/lib/schedule-days";
import {
  calculateAge,
  formatDate,
  Guardian,
  StudentClassEnrollment,
  studentKeys,
  studentStatuses,
  studentStatusLabels,
  studentsApi,
} from "@/lib/students";

const money = (v: number) =>
  new Intl.NumberFormat("tr-TR", { style: "currency", currency: "TRY" }).format(
    v,
  );
const dateTime = (v: string) =>
  new Intl.DateTimeFormat("tr-TR", {
    dateStyle: "medium",
    timeStyle: "short",
    timeZone: "Europe/Istanbul",
  }).format(new Date(v));
const attendanceLabels = {
  Present: "Geldi",
  Absent: "Gelmedi",
  Excused: "Mazeretli",
  Late: "Geç geldi",
  MakeUp: "Telafi",
} as const;
const attendanceClasses = {
  Present: "bg-emerald-50 text-emerald-700",
  Absent: "bg-rose-50 text-rose-700",
  Excused: "bg-sky-50 text-sky-700",
  Late: "bg-amber-50 text-amber-700",
  MakeUp: "bg-violet-50 text-violet-700",
} as const;
const paymentLabels: Record<string, string> = {
  Cash: "Nakit",
  CreditCard: "Kredi kartı",
  BankTransfer: "Havale",
  Other: "Diğer",
};

export function StudentDetailClient() {
  const id = useSearchParams().get("id");
  return isGuid(id) ? <StudentDetailContent id={id} /> : <InvalidStudentId />;
}

function StudentDetailContent({ id }: { id: string }) {
  const router = useRouter(),
    qc = useQueryClient(),
    { data: user } = useCurrentUser();
  const canArchive =
    user?.roles.some((r) => r === "Admin" || r === "Management") ?? false;
  const studentQuery = useQuery({
    queryKey: studentKeys.detail(id),
    queryFn: () => studentsApi.detail(id, canArchive),
    enabled: !!user,
  });
  const financeQuery = useQuery({
    queryKey: operationKeys.section("student-finance", id),
    queryFn: () => operationsApi.studentFinance(id),
    enabled: !!user,
  });
  const attendanceQuery = useQuery({
    queryKey: operationKeys.section("student-attendance", id),
    queryFn: () => operationsApi.studentAttendance(id),
    enabled: !!user,
  });
  const [editOpen, setEditOpen] = useState(false),
    [guardianOpen, setGuardianOpen] = useState(false),
    [classOpen, setClassOpen] = useState(false),
    [membershipOpen, setMembershipOpen] = useState(false),
    [paymentOpen, setPaymentOpen] = useState(false),
    [archiveOpen, setArchiveOpen] = useState(false);
  const [editingGuardian, setEditingGuardian] = useState<Guardian>(),
    [deletingGuardian, setDeletingGuardian] = useState<Guardian>();
  const statusMutation = useMutation({
    mutationFn: (status: (typeof studentStatuses)[number]) =>
      studentsApi.changeStatus(id, status),
    onSuccess: (s) => {
      qc.setQueryData(studentKeys.detail(id), s);
      qc.invalidateQueries({ queryKey: studentKeys.all });
      toast.success("Öğrenci durumu güncellendi.");
    },
    onError: (e) =>
      toast.error(formErrorMessage(e, "Öğrenci durumu güncellenemedi.")),
  });
  const enrollmentMutation = useMutation({
    mutationFn: ({
      item,
      status,
    }: {
      item: StudentClassEnrollment;
      status: "Active" | "Frozen" | "Cancelled";
    }) =>
      classesApi.changeEnrollmentStatus(
        item.classId,
        item.enrollmentId,
        status,
        status === "Cancelled" ? today() : null,
      ),
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: studentKeys.detail(id) });
      toast.success("Sınıf kaydı güncellendi.");
    },
    onError: (e) =>
      toast.error(formErrorMessage(e, "Sınıf kaydı güncellenemedi.")),
  });
  const deleteGuardian = useMutation({
    mutationFn: (guardianId: string) =>
      studentsApi.deleteGuardian(id, guardianId),
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: studentKeys.detail(id) });
      setDeletingGuardian(undefined);
      toast.success("Veli kaydı kaldırıldı.");
    },
    onError: (e) =>
      toast.error(formErrorMessage(e, "Veli kaydı kaldırılamadı.")),
  });
  const archive = useMutation({
    mutationFn: () => studentsApi.archive(id),
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: studentKeys.all });
      toast.success("Öğrenci arşivlendi.");
      router.replace("/students");
    },
    onError: (e) => toast.error(formErrorMessage(e, "Öğrenci arşivlenemedi.")),
  });
  if (studentQuery.isLoading) return <PageLoading />;
  if (studentQuery.isError)
    return <FatalError retry={() => studentQuery.refetch()} />;
  const student = studentQuery.data!,
    name = `${student.firstName} ${student.lastName}`,
    activeClasses = student.classEnrollments.filter(
      (x) => x.status === "Active",
    ).length,
    activeMembership = financeQuery.data?.memberships.find(
      (x) => x.status === "Active",
    ),
    lastAttendance = attendanceQuery.data?.items[0];
  const refreshFinance = () =>
    qc.invalidateQueries({
      queryKey: operationKeys.section("student-finance", id),
    });
  return (
    <div className="mx-auto max-w-7xl">
      <Link
        href="/students"
        className="mb-5 inline-flex items-center gap-2 text-sm text-zinc-500 hover:text-zinc-900"
      >
        <ArrowLeft size={16} />
        Öğrencilere dön
      </Link>
      <header className="flex flex-col gap-5 border-b pb-6 lg:flex-row lg:items-start lg:justify-between">
        <div className="flex items-start gap-4">
          <div className="grid size-14 shrink-0 place-items-center rounded-2xl bg-[#e8eee2] text-lg font-semibold text-[#526743]">
            {student.firstName[0]}
            {student.lastName[0]}
          </div>
          <div>
            <div className="flex flex-wrap items-center gap-3">
              <h1 className="text-2xl font-semibold sm:text-3xl">{name}</h1>
              <StatusBadge status={student.status} />
            </div>
            <div className="mt-2 flex flex-wrap gap-x-5 gap-y-1 text-sm text-zinc-500">
              {student.phone && (
                <a
                  href={`tel:${student.phone}`}
                  className="inline-flex items-center gap-1.5"
                >
                  <Phone size={14} />
                  {student.phone}
                </a>
              )}
              {student.email && (
                <a
                  href={`mailto:${student.email}`}
                  className="inline-flex items-center gap-1.5"
                >
                  <Mail size={14} />
                  {student.email}
                </a>
              )}
              <span>
                Doğum:{" "}
                {student.birthDate
                  ? `${formatDate(student.birthDate)} · ${calculateAge(student.birthDate)} yaş`
                  : "—"}
              </span>
              <span>Kayıt: {formatDate(student.registrationDate)}</span>
            </div>
          </div>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" onClick={() => setEditOpen(true)}>
            <Edit3 />
            Düzenle
          </Button>
          <Button variant="outline" onClick={() => setClassOpen(true)}>
            <GraduationCap />
            Sınıfa ata
          </Button>
          <Button variant="outline" onClick={() => setMembershipOpen(true)}>
            <WalletCards />
            Üyelik ata
          </Button>
          <Button variant="outline" onClick={() => setPaymentOpen(true)}>
            <Banknote />
            Ödeme al
          </Button>
          <Button
            variant="outline"
            onClick={() => {
              setEditingGuardian(undefined);
              setGuardianOpen(true);
            }}
          >
            <Users />
            Veli ekle
          </Button>
        </div>
      </header>
      <div className="my-6 grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <Metric
          label="Aktif sınıf"
          value={String(activeClasses)}
          detail={`${student.classEnrollments.length} toplam kayıt`}
        />
        <Metric
          label="Aktif üyelik"
          value={
            financeQuery.isLoading
              ? "Yükleniyor"
              : (activeMembership?.planName ?? "Yok")
          }
          detail={
            activeMembership
              ? formatDate(activeMembership.endDate)
              : "Aktif üyelik bulunmuyor"
          }
        />
        <Metric
          label="Açık bakiye"
          value={
            financeQuery.isLoading
              ? "Yükleniyor"
              : financeQuery.data
                ? money(financeQuery.data.openBalance)
                : "—"
          }
          detail={
            financeQuery.data?.overdueBalance
              ? `${money(financeQuery.data.overdueBalance)} gecikmiş`
              : "Gecikmiş bakiye yok"
          }
        />
        <Metric
          label="Yoklama oranı"
          value={
            attendanceQuery.isLoading
              ? "Yükleniyor"
              : attendanceQuery.data
                ? `%${attendanceQuery.data.attendanceRate}`
                : "—"
          }
          detail={
            lastAttendance
              ? `Son: ${attendanceLabels[lastAttendance.status]}`
              : "Yoklama kaydı yok"
          }
        />
      </div>
      <div className="grid gap-6 lg:grid-cols-[minmax(0,1.65fr)_minmax(280px,.8fr)]">
        <main className="space-y-6">
          <ClassesSection
            items={student.classEnrollments}
            pending={enrollmentMutation.isPending}
            error={enrollmentMutation.error}
            onChange={(item, status) =>
              enrollmentMutation.mutate({ item, status })
            }
            onAdd={() => setClassOpen(true)}
          />
          <FinanceSection
            query={financeQuery}
            onRetry={() => financeQuery.refetch()}
            onMembership={() => setMembershipOpen(true)}
            onPayment={() => setPaymentOpen(true)}
          />
          <AttendanceSection
            query={attendanceQuery}
            studentId={id}
            onRetry={() => attendanceQuery.refetch()}
          />
        </main>
        <aside className="space-y-6">
          <Section
            title="Veliler ve yakınlar"
            icon={<Users size={18} />}
            action={
              <Button
                size="sm"
                variant="outline"
                onClick={() => {
                  setEditingGuardian(undefined);
                  setGuardianOpen(true);
                }}
              >
                <Plus />
                Ekle
              </Button>
            }
          >
            {student.guardians.length === 0 ? (
              <Empty text="Henüz veli veya yakın bilgisi eklenmemiş." />
            ) : (
              <div className="divide-y">
                {student.guardians.map((g) => (
                  <div
                    key={g.id}
                    className="flex items-center gap-2 py-3 first:pt-0 last:pb-0"
                  >
                    <div className="min-w-0 flex-1">
                      <p className="font-medium">
                        {g.firstName} {g.lastName}
                      </p>
                      <p className="text-sm text-zinc-500">
                        {g.relationship} · {g.phone ?? "Telefon yok"}
                      </p>
                    </div>
                    <Button
                      size="sm"
                      variant="ghost"
                      onClick={() => {
                        setEditingGuardian(g);
                        setGuardianOpen(true);
                      }}
                    >
                      Düzenle
                    </Button>
                    <Button
                      size="icon-sm"
                      variant="ghost"
                      className="text-red-600"
                      aria-label="Veli kaydını kaldır"
                      onClick={() => setDeletingGuardian(g)}
                    >
                      <Trash2 />
                    </Button>
                  </div>
                ))}
              </div>
            )}
          </Section>
          <Section title="Öğrenci bilgileri" icon={<UserRound size={18} />}>
            <dl className="grid gap-4 sm:grid-cols-2">
              <Info label="Ad soyad" value={name} />
              <Info
                label="Doğum tarihi"
                value={
                  student.birthDate
                    ? `${formatDate(student.birthDate)} · ${calculateAge(student.birthDate)} yaş`
                    : "—"
                }
              />
              <Info label="Cinsiyet" value={student.gender ?? "—"} />
              <Info label="Telefon" value={student.phone ?? "—"} />
              <Info label="E-posta" value={student.email ?? "—"} />
              <Info
                label="Kayıt tarihi"
                value={formatDate(student.registrationDate)}
              />
              <Info label="Notlar" value={student.notes || "Not eklenmemiş."} />
            </dl>
          </Section>
          <Section title="Öğrenci durumu" icon={<CalendarDays size={18} />}>
            <label
              htmlFor="student-status"
              className="mb-2 block text-xs font-medium uppercase text-zinc-400"
            >
              Operasyonel durum
            </label>
            <select
              id="student-status"
              value={student.status}
              disabled={statusMutation.isPending}
              onChange={(e) =>
                statusMutation.mutate(
                  e.target.value as (typeof studentStatuses)[number],
                )
              }
              className="control"
            >
              {studentStatuses.map((s) => (
                <option key={s} value={s}>
                  {studentStatusLabels[s]}
                </option>
              ))}
            </select>
            {statusMutation.isPending && (
              <p className="mt-2 text-xs text-zinc-500">
                Durum güncelleniyor...
              </p>
            )}
            {statusMutation.isError && (
              <InlineError error={statusMutation.error} />
            )}{" "}
            <p className="mt-3 text-xs leading-5 text-zinc-500">
              Durum değişikliği öğrenciyi veya geçmiş kayıtlarını silmez.
            </p>
            {canArchive && (
              <Button
                variant="outline"
                className="mt-4 w-full text-amber-700"
                onClick={() => setArchiveOpen(true)}
              >
                <Archive />
                Arşivle
              </Button>
            )}
          </Section>
        </aside>
      </div>
      <StudentFormDialog
        open={editOpen}
        onOpenChange={setEditOpen}
        student={student}
      />
      <GuardianFormDialog
        studentId={id}
        guardian={editingGuardian}
        open={guardianOpen}
        onOpenChange={setGuardianOpen}
      />
      <StudentClassAssignmentDialog
        student={{ id, name }}
        open={classOpen}
        onOpenChange={setClassOpen}
      />
      <MembershipDialog
        student={{ id, name }}
        open={membershipOpen}
        onOpenChange={setMembershipOpen}
        onSaved={refreshFinance}
      />
      <PaymentDialog
        student={{ id, name }}
        open={paymentOpen}
        onOpenChange={setPaymentOpen}
        onSaved={refreshFinance}
      />
      <AlertDialog
        open={!!deletingGuardian}
        onOpenChange={(open) => !open && setDeletingGuardian(undefined)}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Veli kaydı kaldırılsın mı?</AlertDialogTitle>
            <AlertDialogDescription>
              {deletingGuardian?.firstName} {deletingGuardian?.lastName} veli
              listesinden kaldırılacak.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Vazgeç</AlertDialogCancel>
            <AlertDialogAction
              variant="destructive"
              disabled={deleteGuardian.isPending}
              onClick={() =>
                deletingGuardian && deleteGuardian.mutate(deletingGuardian.id)
              }
            >
              Kaydı kaldır
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
      <AlertDialog open={archiveOpen} onOpenChange={setArchiveOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Öğrenci arşivlensin mi?</AlertDialogTitle>
            <AlertDialogDescription>
              Tarihsel sınıf, finans ve yoklama kayıtları korunacaktır.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Vazgeç</AlertDialogCancel>
            <AlertDialogAction
              disabled={archive.isPending}
              onClick={() => archive.mutate()}
            >
              Arşivle
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}

const today = () =>
  new Intl.DateTimeFormat("en-CA", { timeZone: "Europe/Istanbul" }).format(
    new Date(),
  );

function ClassesSection({
  items,
  pending,
  error,
  onChange,
  onAdd,
}: {
  items: StudentClassEnrollment[];
  pending: boolean;
  error: unknown;
  onChange: (
    item: StudentClassEnrollment,
    status: "Active" | "Frozen" | "Cancelled",
  ) => void;
  onAdd: () => void;
}) {
  return (
    <Section
      title="Sınıflar"
      icon={<GraduationCap size={18} />}
      action={
        <Button size="sm" variant="outline" onClick={onAdd}>
          <Plus />
          Sınıfa ata
        </Button>
      }
    >
      {items.length === 0 ? (
        <Empty text="Henüz sınıf kaydı bulunmuyor." />
      ) : (
        <div className="space-y-3">
          {items.map((item) => (
            <div key={item.enrollmentId} className="rounded-lg border p-4">
              <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                <div>
                  <Link
                    href={classDetailHref(item.classId)}
                    className="font-medium hover:underline"
                  >
                    {item.className}
                  </Link>
                  <p className="mt-1 text-sm text-zinc-500">
                    {item.instructorName} · {item.roomName}
                  </p>
                  <p className="mt-2 text-xs text-zinc-500">
                    {item.schedules
                      .map((x) =>
                        scheduleDayTimeText(
                          x.dayOfWeek,
                          x.startTime,
                          x.endTime,
                        ),
                      )
                      .join(", ") || "Program yok"}
                  </p>
                  <p className="mt-1 text-xs text-zinc-400">
                    {formatDate(item.startDate)} – {formatDate(item.endDate)} ·{" "}
                    {enrollmentStatusLabels[item.status]}
                  </p>
                </div>
                <div className="flex flex-wrap gap-2">
                  <Button
                    size="sm"
                    variant="outline"
                    render={<Link href={classDetailHref(item.classId)} />}
                  >
                    Detay
                  </Button>
                  {item.status === "Active" && (
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={pending}
                      onClick={() => onChange(item, "Frozen")}
                    >
                      Dondur
                    </Button>
                  )}
                  {item.status === "Frozen" && (
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={pending}
                      onClick={() => onChange(item, "Active")}
                    >
                      Aktifleştir
                    </Button>
                  )}
                  {(item.status === "Active" || item.status === "Frozen") && (
                    <Button
                      size="sm"
                      variant="destructive"
                      disabled={pending}
                      onClick={() => onChange(item, "Cancelled")}
                    >
                      Kaydı kaldır
                    </Button>
                  )}
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
      {!!error && <InlineError error={error} />}
    </Section>
  );
}

function FinanceSection({
  query,
  onRetry,
  onMembership,
  onPayment,
}: {
  query: {
    data?: StudentFinanceOverview;
    isLoading: boolean;
    isError: boolean;
    error: unknown;
  };
  onRetry: () => void;
  onMembership: () => void;
  onPayment: () => void;
}) {
  return (
    <Section
      title="Üyelik ve finans"
      icon={<ReceiptText size={18} />}
      action={
        <div className="flex gap-2">
          <Button size="sm" variant="outline" onClick={onMembership}>
            Üyelik ata
          </Button>
          <Button size="sm" onClick={onPayment}>
            Ödeme al
          </Button>
        </div>
      }
    >
      {query.isLoading ? (
        <SectionLoading />
      ) : query.isError ? (
        <SectionError error={query.error} retry={onRetry} />
      ) : (
        <>
          <div className="grid gap-3 sm:grid-cols-4">
            <SmallStat
              label="Toplam borç"
              value={money(query.data!.totalInvoiced)}
            />
            <SmallStat label="Ödenen" value={money(query.data!.totalPaid)} />
            <SmallStat label="Açık" value={money(query.data!.openBalance)} />
            <SmallStat
              label="Gecikmiş"
              value={money(query.data!.overdueBalance)}
            />
          </div>
          <h3 className="mt-5 text-sm font-semibold">Üyelikler</h3>
          {query.data!.memberships.length === 0 ? (
            <Empty text="Üyelik kaydı bulunmuyor." compact />
          ) : (
            <div className="mt-2 space-y-2">
              {query.data!.memberships.map((x) => (
                <div
                  key={x.id}
                  className="flex flex-wrap justify-between gap-2 rounded-lg bg-zinc-50 p-3 text-sm"
                >
                  <span>
                    <b>{x.planName}</b> · {x.status}
                  </span>
                  <span>
                    {money(x.price)}
                    {x.discountAmount
                      ? ` · ${money(x.discountAmount)} indirim`
                      : ""}
                  </span>
                </div>
              ))}
            </div>
          )}
          <div className="mt-5 grid gap-5 md:grid-cols-2">
            <History
              title="Son faturalar"
              empty="Fatura yok"
              items={query.data!.invoices.map((x) => (
                <div key={x.id} className="border-b py-2 text-sm last:border-0">
                  <div className="flex justify-between gap-3">
                    <span>{x.description}</span>
                    <b>{money(x.remaining)} açık</b>
                  </div>
                  <p className="text-xs text-zinc-500">
                    Vade {formatDate(x.dueDate)} · {x.status}
                  </p>
                </div>
              ))}
            />
            <History
              title="Son ödemeler"
              empty="Ödeme yok"
              items={query.data!.payments.map((x) => (
                <div key={x.id} className="border-b py-2 text-sm last:border-0">
                  <div className="flex justify-between gap-3">
                    <span>{x.invoiceDescription ?? "Serbest ödeme"}</span>
                    <b>{money(x.amount)}</b>
                  </div>
                  <p className="text-xs text-zinc-500">
                    {dateTime(x.paymentDate)} ·{" "}
                    {paymentLabels[x.paymentMethod] ?? x.paymentMethod}
                  </p>
                </div>
              ))}
            />
          </div>
        </>
      )}
    </Section>
  );
}

function AttendanceSection({
  query,
  studentId,
  onRetry,
}: {
  query: {
    data?: StudentAttendanceHistory;
    isLoading: boolean;
    isError: boolean;
    error: unknown;
  };
  studentId: string;
  onRetry: () => void;
}) {
  return (
    <Section
      title="Yoklama"
      icon={<CalendarCheck size={18} />}
      action={
        <Button
          size="sm"
          variant="outline"
          render={<Link href={`/attendance?studentId=${studentId}`} />}
        >
          Yoklamaya git
        </Button>
      }
    >
      {query.isLoading ? (
        <SectionLoading />
      ) : query.isError ? (
        <SectionError error={query.error} retry={onRetry} />
      ) : (
        <>
          <div className="grid grid-cols-3 gap-3 sm:grid-cols-7">
            <SmallStat label="Toplam" value={String(query.data!.total)} />
            <SmallStat label="Oran" value={`%${query.data!.attendanceRate}`} />
            <SmallStat label="Geldi" value={String(query.data!.present)} />
            <SmallStat label="Gelmedi" value={String(query.data!.absent)} />
            <SmallStat label="Mazeret" value={String(query.data!.excused)} />
            <SmallStat label="Geç" value={String(query.data!.late)} />
            <SmallStat label="Telafi" value={String(query.data!.makeUp)} />
          </div>
          <h3 className="mt-5 text-sm font-semibold">Son 10 yoklama</h3>
          {query.data!.items.length === 0 ? (
            <Empty text="Henüz yoklama kaydı bulunmuyor." compact />
          ) : (
            <div className="mt-2 divide-y">
              {query.data!.items.map((x) => (
                <div
                  key={x.attendanceId}
                  className="flex items-center justify-between gap-3 py-3 text-sm"
                >
                  <div>
                    <Link
                      href={classDetailHref(x.classId)}
                      className="font-medium hover:underline"
                    >
                      {x.className}
                    </Link>
                    <p className="text-xs text-zinc-500">
                      {dateTime(x.scheduledStart)}
                      {x.notes ? ` · ${x.notes}` : ""}
                    </p>
                  </div>
                  <span
                    className={`rounded-md px-2 py-1 text-xs ${attendanceClasses[x.status]}`}
                  >
                    {attendanceLabels[x.status]}
                  </span>
                </div>
              ))}
            </div>
          )}
        </>
      )}
    </Section>
  );
}

function Section({
  title,
  icon,
  action,
  children,
}: {
  title: string;
  icon: React.ReactNode;
  action?: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <section className="rounded-xl border bg-white p-5 shadow-sm sm:p-6">
      <div className="mb-5 flex flex-wrap items-center justify-between gap-3">
        <h2 className="flex items-center gap-2 font-semibold">
          {icon}
          {title}
        </h2>
        {action}
      </div>
      {children}
    </section>
  );
}
function Metric({
  label,
  value,
  detail,
}: {
  label: string;
  value: string;
  detail: string;
}) {
  return (
    <div className="rounded-xl border bg-white p-4">
      <p className="text-xs font-medium uppercase text-zinc-400">{label}</p>
      <p className="mt-2 truncate text-xl font-semibold">{value}</p>
      <p className="mt-1 truncate text-xs text-zinc-500">{detail}</p>
    </div>
  );
}
function SmallStat({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg bg-zinc-50 p-3">
      <p className="text-xs text-zinc-500">{label}</p>
      <p className="mt-1 font-semibold">{value}</p>
    </div>
  );
}
function History({
  title,
  empty,
  items,
}: {
  title: string;
  empty: string;
  items: React.ReactNode[];
}) {
  return (
    <div>
      <h3 className="text-sm font-semibold">{title}</h3>
      <div className="mt-2">
        {items.length ? (
          items
        ) : (
          <p className="text-sm text-zinc-500">{empty}</p>
        )}
      </div>
    </div>
  );
}
function Info({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs font-medium uppercase text-zinc-400">{label}</dt>
      <dd className="mt-1 whitespace-pre-wrap text-sm text-zinc-800">
        {value}
      </dd>
    </div>
  );
}
function Empty({ text, compact = false }: { text: string; compact?: boolean }) {
  return (
    <div
      className={`rounded-lg border border-dashed bg-zinc-50 px-4 text-center text-sm text-zinc-500 ${compact ? "mt-2 py-4" : "py-7"}`}
    >
      {text}
    </div>
  );
}
function SectionLoading() {
  return (
    <div className="space-y-3" aria-label="Yükleniyor">
      <div className="h-16 animate-pulse rounded-lg bg-zinc-100" />
      <div className="h-24 animate-pulse rounded-lg bg-zinc-100" />
    </div>
  );
}
function SectionError({ error, retry }: { error: unknown; retry: () => void }) {
  return (
    <div
      role="alert"
      className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700"
    >
      <p>
        {formErrorMessage(
          error,
          "Bu bölüm yüklenirken beklenmeyen bir hata oluştu.",
        )}
      </p>
      <Button size="sm" variant="outline" className="mt-3" onClick={retry}>
        <RefreshCw />
        Tekrar dene
      </Button>
    </div>
  );
}
function InlineError({ error }: { error: unknown }) {
  return (
    <p role="alert" className="mt-3 text-sm text-red-700">
      {formErrorMessage(error, "İşlem tamamlanamadı.")}
    </p>
  );
}
function PageLoading() {
  return (
    <div className="mx-auto max-w-7xl space-y-5">
      <div className="h-28 animate-pulse rounded-xl bg-zinc-200" />
      <div className="h-80 animate-pulse rounded-xl bg-zinc-100" />
    </div>
  );
}
function FatalError({ retry }: { retry: () => void }) {
  return (
    <div className="mx-auto grid min-h-[60vh] max-w-3xl place-items-center text-center">
      <div>
        <ShieldAlert className="mx-auto mb-4 text-red-500" />
        <h1 className="text-xl font-semibold">Öğrenci bilgileri açılamadı</h1>
        <p className="mt-2 text-sm text-zinc-500">
          Kayıt bulunamadı, bağlantı kurulamadı veya erişim yetkiniz yok.
        </p>
        <div className="mt-5 flex justify-center gap-2">
          <Button variant="outline" render={<Link href="/students" />}>
            Öğrencilere dön
          </Button>
          <Button onClick={retry}>
            <RefreshCw />
            Tekrar dene
          </Button>
        </div>
      </div>
    </div>
  );
}
function InvalidStudentId() {
  return (
    <div className="mx-auto grid min-h-[60vh] max-w-3xl place-items-center text-center">
      <div>
        <ShieldAlert className="mx-auto mb-4 text-amber-600" />
        <h1 className="text-xl font-semibold">Geçersiz öğrenci bağlantısı</h1>
        <Button
          className="mt-5"
          variant="outline"
          render={<Link href="/students" />}
        >
          Öğrencilere dön
        </Button>
      </div>
    </div>
  );
}
