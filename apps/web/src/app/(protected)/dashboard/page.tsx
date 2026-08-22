"use client";

import { useQuery, useQueryClient } from "@tanstack/react-query";
import {
  AlertTriangle,
  ArrowRight,
  Banknote,
  CalendarCheck,
  GraduationCap,
  Plus,
  ReceiptText,
  RefreshCw,
  UserPlus,
  Users,
  WalletCards,
} from "lucide-react";
import Link from "next/link";
import { useState } from "react";
import {
  MembershipDialog,
  PaymentDialog,
} from "@/components/operations/finance-dialogs";
import { StudentClassAssignmentDialog } from "@/components/students/student-class-assignment-dialog";
import { StudentFormDialog } from "@/components/students/student-form-dialog";
import { Button } from "@/components/ui/button";
import { useCurrentUser } from "@/hooks/use-current-user";
import { formErrorMessage } from "@/lib/form-errors";
import {
  DashboardLesson,
  operationKeys,
  operationsApi,
} from "@/lib/operations";
import { classDetailHref } from "@/lib/routes";

const money = (value: number) =>
  new Intl.NumberFormat("tr-TR", {
    style: "currency",
    currency: "TRY",
    maximumFractionDigits: 0,
  }).format(value);
const time = (value: string) =>
  new Intl.DateTimeFormat("tr-TR", {
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
    timeZone: "Europe/Istanbul",
  }).format(new Date(value));

export default function DashboardPage() {
  const qc = useQueryClient(),
    { data: user } = useCurrentUser();
  const operations = useQuery({
    queryKey: operationKeys.section("dashboard-operations"),
    queryFn: operationsApi.dashboardOperations,
  });
  const analytics = useQuery({
    queryKey: operationKeys.section("dashboard-analytics"),
    queryFn: operationsApi.dashboardAnalytics,
  });
  const [studentOpen, setStudentOpen] = useState(false),
    [paymentOpen, setPaymentOpen] = useState(false),
    [membershipOpen, setMembershipOpen] = useState(false),
    [assignmentOpen, setAssignmentOpen] = useState(false);
  const canManage =
    user?.roles.some((role) =>
      ["Admin", "Management", "Reception"].includes(role),
    ) ?? false;
  const refresh = () => {
    qc.invalidateQueries({
      queryKey: operationKeys.section("dashboard-operations"),
    });
    qc.invalidateQueries({
      queryKey: operationKeys.section("dashboard-analytics"),
    });
  };
  return (
    <div className="mx-auto max-w-7xl space-y-6">
      <header>
        <p className="text-sm font-medium text-[#61734f]">Günlük operasyon</p>
        <h1 className="mt-1 text-2xl font-semibold tracking-tight sm:text-3xl">
          BORDER Panel
        </h1>
        <p className="mt-2 text-sm text-zinc-500">
          Bugünün derslerini, finansal durumu ve dikkat gerektiren işleri tek
          ekrandan yönetin.
        </p>
      </header>
      <div className="grid grid-cols-2 gap-3 xl:grid-cols-4">
        <Kpi
          href="/students"
          label="Aktif öğrenci"
          value={
            operations.data ? String(operations.data.activeStudentCount) : null
          }
          loading={operations.isLoading}
          icon={<Users />}
        />
        <Kpi
          href="/schedule"
          label="Bugünkü ders"
          value={
            operations.data ? String(operations.data.todayLessonCount) : null
          }
          loading={operations.isLoading}
          icon={<CalendarCheck />}
        />
        {analytics.data?.canViewFinance !== false ? (
          <>
            <Kpi
              href="/payments"
              label="Bu ay tahsilat"
              value={
                analytics.data ? money(analytics.data.monthlyRevenue) : null
              }
              loading={analytics.isLoading}
              icon={<Banknote />}
            />
            <Kpi
              href="/balances"
              label="Açık bakiye"
              value={
                analytics.data ? money(analytics.data.outstandingBalance) : null
              }
              loading={analytics.isLoading}
              icon={<ReceiptText />}
            />
          </>
        ) : (
          <>
            <Kpi
              href="/attendance"
              label="Son 30 gün devam"
              value={
                analytics.data ? `%${analytics.data.attendanceRate}` : null
              }
              loading={analytics.isLoading}
              icon={<CalendarCheck />}
            />
            <Kpi
              href="/my-classes"
              label="Yeni öğrenci"
              value={analytics.data ? String(analytics.data.newStudents) : null}
              loading={analytics.isLoading}
              icon={<UserPlus />}
            />
          </>
        )}
      </div>
      <div className="grid gap-6 lg:grid-cols-[minmax(0,1.8fr)_minmax(280px,.7fr)]">
        <Panel
          title="Bugünün programı"
          detail="Europe/Istanbul saat sırasına göre"
          action={
            <Button
              size="sm"
              variant="outline"
              render={<Link href="/schedule" />}
            >
              Tüm program
              <ArrowRight />
            </Button>
          }
        >
          {operations.isLoading ? (
            <RowsSkeleton />
          ) : operations.isError ? (
            <ErrorState
              error={operations.error}
              retry={() => operations.refetch()}
            />
          ) : operations.data!.todayLessons.length === 0 ? (
            <Empty
              title="Bugün planlanmış ders yok"
              detail="Gününüz boş görünüyor. Program ekranından haftalık planı inceleyebilirsiniz."
            />
          ) : (
            <div className="divide-y">
              {operations.data!.todayLessons.map((lesson) => (
                <LessonRow key={lesson.sessionId} lesson={lesson} />
              ))}
            </div>
          )}
        </Panel>
        <Panel
          title="Dikkat gerekenler"
          detail="Öncelikli operasyon sinyalleri"
        >
          {analytics.isLoading ? (
            <RowsSkeleton compact />
          ) : analytics.isError ? (
            <ErrorState
              error={analytics.error}
              retry={() => analytics.refetch()}
            />
          ) : analytics.data!.alerts.length === 0 ? (
            <Empty
              title="Kritik durum yok"
              detail="Şu anda dikkat gerektiren kritik bir durum yok."
              compact
            />
          ) : (
            <div className="space-y-2">
              {analytics.data!.alerts.map((alert) => (
                <Link
                  key={alert.type}
                  href={alert.href}
                  className="flex items-center gap-3 rounded-lg border border-amber-100 bg-amber-50/70 p-3 text-sm text-amber-900 hover:border-amber-300"
                >
                  <AlertTriangle className="size-4 shrink-0" />
                  <span className="flex-1">{alert.label}</span>
                  <ArrowRight className="size-4" />
                </Link>
              ))}
            </div>
          )}
        </Panel>
      </div>
      <div className="flex flex-col gap-6">
        <section className="order-2 lg:order-1">
          <AnalyticsPanel
            data={analytics.data}
            loading={analytics.isLoading}
            error={analytics.error}
            retry={() => analytics.refetch()}
          />
        </section>
        <section className="order-1 lg:order-2">
          <Panel
            title="Hızlı işlemler"
            detail="Sık kullanılan işlemleri sayfadan ayrılmadan başlatın"
          >
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
              {canManage && (
                <>
                  <Quick
                    icon={<UserPlus />}
                    label="Öğrenci ekle"
                    onClick={() => setStudentOpen(true)}
                  />
                  <Quick
                    icon={<Banknote />}
                    label="Ödeme al"
                    onClick={() => setPaymentOpen(true)}
                  />
                  <Quick
                    icon={<WalletCards />}
                    label="Üyelik oluştur"
                    onClick={() => setMembershipOpen(true)}
                  />
                  <Quick
                    icon={<GraduationCap />}
                    label="Sınıfa öğrenci ata"
                    onClick={() => setAssignmentOpen(true)}
                  />
                </>
              )}
              <Link
                href="/attendance"
                className="flex min-h-20 items-center gap-3 rounded-xl border bg-zinc-50 p-4 text-sm font-medium hover:border-[#718360] hover:bg-[#f3f6f0]"
              >
                <CalendarCheck className="text-[#61734f]" />
                Yoklama aç
                <ArrowRight className="ml-auto text-zinc-400" />
              </Link>
            </div>
          </Panel>
        </section>
      </div>
      <StudentFormDialog open={studentOpen} onOpenChange={setStudentOpen} />
      <PaymentDialog
        open={paymentOpen}
        onOpenChange={setPaymentOpen}
        onSaved={refresh}
      />
      <MembershipDialog
        open={membershipOpen}
        onOpenChange={setMembershipOpen}
        onSaved={refresh}
      />
      <StudentClassAssignmentDialog
        open={assignmentOpen}
        onOpenChange={setAssignmentOpen}
      />
    </div>
  );
}

function LessonRow({ lesson }: { lesson: DashboardLesson }) {
  return (
    <div className="flex flex-col gap-3 py-4 first:pt-0 last:pb-0 sm:flex-row sm:items-center">
      <div className="w-28 shrink-0">
        <p className="font-semibold tabular-nums">
          {time(lesson.scheduledStart)}–{time(lesson.scheduledEnd)}
        </p>
        <p className="mt-1 text-xs text-zinc-400">
          {lesson.studentCount}/{lesson.capacity} doluluk
        </p>
      </div>
      <div className="min-w-0 flex-1">
        <Link
          href={classDetailHref(lesson.classId)}
          className="font-medium hover:underline"
        >
          {lesson.className}
        </Link>
        <p className="mt-1 truncate text-sm text-zinc-500">
          {lesson.instructorName} · {lesson.roomName}
        </p>
      </div>
      <span
        className={`w-fit rounded-md px-2 py-1 text-xs font-medium ${lesson.isAttendanceCompleted ? "bg-emerald-50 text-emerald-700" : "bg-amber-50 text-amber-700"}`}
      >
        {lesson.isAttendanceCompleted
          ? "Yoklama tamamlandı"
          : "Yoklama alınmadı"}
      </span>
      <div className="flex gap-2">
        <Button
          size="sm"
          variant="outline"
          render={<Link href={classDetailHref(lesson.classId)} />}
        >
          Sınıfa git
        </Button>
        <Button
          size="sm"
          render={<Link href={`/attendance?sessionId=${lesson.sessionId}`} />}
        >
          {lesson.isAttendanceCompleted ? "Yoklamayı aç" : "Yoklama al"}
        </Button>
      </div>
    </div>
  );
}

function AnalyticsPanel({
  data,
  loading,
  error,
  retry,
}: {
  data:
    Awaited<ReturnType<typeof operationsApi.dashboardAnalytics>> | undefined;
  loading: boolean;
  error: unknown;
  retry: () => void;
}) {
  return (
    <Panel title="Son 30 gün" detail="Tahsilat ve devam görünümü">
      {loading ? (
        <RowsSkeleton />
      ) : error ? (
        <ErrorState error={error} retry={retry} />
      ) : (
        <div className="grid gap-6 lg:grid-cols-[minmax(0,1.7fr)_minmax(260px,.8fr)]">
          <div>
            <div className="flex h-40 items-end gap-1 rounded-xl bg-zinc-50 p-4">
              {data!.thirtyDayRevenue.map((point, index) => {
                const max = Math.max(
                  ...data!.thirtyDayRevenue.map((x) => x.value),
                  1,
                );
                return (
                  <div
                    key={`${point.label}-${index}`}
                    className="group relative flex h-full min-w-0 flex-1 items-end"
                    title={`${point.label}: ${money(point.value)}`}
                  >
                    <div
                      className="w-full rounded-t bg-[#718360] transition-colors group-hover:bg-[#526743]"
                      style={{
                        height: `${Math.max(point.value ? 5 : 1, (100 * point.value) / max)}%`,
                      }}
                    />
                  </div>
                );
              })}
            </div>
            <div className="mt-2 flex justify-between text-xs text-zinc-400">
              <span>30 gün önce</span>
              <span>Bugün</span>
            </div>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <MiniMetric
              label="Yeni öğrenci"
              value={String(data!.newStudents)}
            />
            <AttendanceGauge rate={data!.attendanceRate} />
            {data!.canViewFinance && (
              <>
                <MiniMetric
                  label="Toplam ödeme"
                  value={money(data!.totalPayments)}
                />
                <MiniMetric
                  label="Aktif üyelik"
                  value={String(data!.activeMemberships)}
                />
              </>
            )}
          </div>
        </div>
      )}
    </Panel>
  );
}
function Kpi({
  href,
  label,
  value,
  loading,
  icon,
}: {
  href: string;
  label: string;
  value: string | null;
  loading: boolean;
  icon: React.ReactNode;
}) {
  return (
    <Link
      href={href}
      className="flex min-h-28 flex-col justify-between rounded-xl border bg-white p-4 shadow-sm transition-colors hover:border-[#879878]"
    >
      <div className="flex items-center justify-between gap-2">
        <p className="text-xs font-medium uppercase tracking-wide text-zinc-400">
          {label}
        </p>
        <span className="text-[#61734f] [&_svg]:size-4">{icon}</span>
      </div>
      {loading ? (
        <div className="h-7 w-24 animate-pulse rounded bg-zinc-100" />
      ) : (
        <p className="text-xl font-semibold sm:text-2xl">{value ?? "—"}</p>
      )}
    </Link>
  );
}
function Panel({
  title,
  detail,
  action,
  children,
}: {
  title: string;
  detail: string;
  action?: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <section className="rounded-xl border bg-white p-5 shadow-sm sm:p-6">
      <div className="mb-5 flex items-start justify-between gap-3">
        <div>
          <h2 className="font-semibold">{title}</h2>
          <p className="mt-1 text-xs text-zinc-500">{detail}</p>
        </div>
        {action}
      </div>
      {children}
    </section>
  );
}
function Quick({
  icon,
  label,
  onClick,
}: {
  icon: React.ReactNode;
  label: string;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="flex min-h-20 items-center gap-3 rounded-xl border bg-zinc-50 p-4 text-left text-sm font-medium hover:border-[#718360] hover:bg-[#f3f6f0]"
    >
      <span className="text-[#61734f]">{icon}</span>
      {label}
      <Plus className="ml-auto size-4 text-zinc-400" />
    </button>
  );
}
function MiniMetric({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl bg-zinc-50 p-4">
      <p className="text-xs text-zinc-500">{label}</p>
      <p className="mt-2 text-lg font-semibold">{value}</p>
    </div>
  );
}
function AttendanceGauge({ rate }: { rate: number }) {
  return (
    <div className="rounded-xl bg-zinc-50 p-3">
      <p className="text-xs text-zinc-500">Ort. devam</p>
      <div className="mt-2 flex items-center gap-3">
        <div
          role="img"
          aria-label={`Devam oranı yüzde ${rate}`}
          className="grid size-12 place-items-center rounded-full"
          style={{
            background: `conic-gradient(#718360 ${Math.min(100, Math.max(0, rate))}%, #e4e4e7 0)`,
          }}
        >
          <div className="size-8 rounded-full bg-zinc-50" />
        </div>
        <p className="text-lg font-semibold">%{rate}</p>
      </div>
    </div>
  );
}
function RowsSkeleton({ compact = false }: { compact?: boolean }) {
  return (
    <div className="space-y-3" aria-label="Yükleniyor">
      {Array.from({ length: compact ? 3 : 4 }, (_, i) => (
        <div key={i} className="h-14 animate-pulse rounded-lg bg-zinc-100" />
      ))}
    </div>
  );
}
function ErrorState({ error, retry }: { error: unknown; retry: () => void }) {
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
function Empty({
  title,
  detail,
  compact = false,
}: {
  title: string;
  detail: string;
  compact?: boolean;
}) {
  return (
    <div
      className={`rounded-lg border border-dashed bg-zinc-50 text-center ${compact ? "p-4" : "p-7"}`}
    >
      <p className="text-sm font-medium">{title}</p>
      <p className="mt-1 text-xs text-zinc-500">{detail}</p>
    </div>
  );
}
