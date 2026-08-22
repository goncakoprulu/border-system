"use client";

import { useQuery } from "@tanstack/react-query";
import { ArrowRight, Download, RefreshCw } from "lucide-react";
import Link from "next/link";
import { useMemo, useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { classesApi, classKeys } from "@/lib/classes";
import { formErrorMessage } from "@/lib/form-errors";
import {
  ReportCapacity,
  ReportEngagement,
  ReportFinance,
  ReportMetric,
  ReportPoint,
  reportKeys,
  reportsApi,
} from "@/lib/reports";
import { classDetailHref, studentDetailHref } from "@/lib/routes";

type Preset =
  "thisMonth" | "lastMonth" | "last30" | "last3Months" | "thisYear" | "custom";
const money = (value: number) =>
  new Intl.NumberFormat("tr-TR", {
    style: "currency",
    currency: "TRY",
    maximumFractionDigits: 0,
  }).format(value);
const today = () =>
  new Intl.DateTimeFormat("en-CA", { timeZone: "Europe/Istanbul" }).format(
    new Date(),
  );
const iso = (date: Date) => date.toISOString().slice(0, 10);
const rangeFor = (preset: Preset) => {
  const current = today(),
    [year, month, day] = current.split("-").map(Number),
    end = new Date(Date.UTC(year, month - 1, day));
  if (preset === "thisMonth")
    return {
      from: `${year}-${String(month).padStart(2, "0")}-01`,
      to: current,
    };
  if (preset === "lastMonth")
    return {
      from: iso(new Date(Date.UTC(year, month - 2, 1))),
      to: iso(new Date(Date.UTC(year, month - 1, 0))),
    };
  if (preset === "last30") {
    const start = new Date(end);
    start.setUTCDate(start.getUTCDate() - 29);
    return { from: iso(start), to: current };
  }
  if (preset === "last3Months") {
    const start = new Date(Date.UTC(year, month - 3, 1));
    return { from: iso(start), to: current };
  }
  return { from: `${year}-01-01`, to: current };
};
const statusLabels: Record<string, string> = {
  Lead: "Aday",
  Trial: "Deneme",
  Active: "Aktif",
  Frozen: "Donduruldu",
  Passive: "Pasif",
  Left: "Ayrıldı",
  Pending: "Bekliyor",
  PartiallyPaid: "Kısmi ödendi",
  Paid: "Ödendi",
  Cancelled: "İptal",
};
const methodLabels: Record<string, string> = {
  Cash: "Nakit",
  CreditCard: "Kredi kartı",
  BankTransfer: "Havale",
  Other: "Diğer",
};

export function ReportsSection() {
  const initial = rangeFor("thisMonth"),
    [preset, setPreset] = useState<Preset>("thisMonth"),
    [from, setFrom] = useState(initial.from),
    [to, setTo] = useState(initial.to),
    [instructorId, setInstructorId] = useState(""),
    [classId, setClassId] = useState(""),
    [roomId, setRoomId] = useState("");
  const params = useMemo(() => {
    const p = new URLSearchParams({ from, to });
    if (instructorId) p.set("instructorId", instructorId);
    if (classId) p.set("classId", classId);
    if (roomId) p.set("roomId", roomId);
    return p.toString();
  }, [from, to, instructorId, classId, roomId]);
  const summary = useQuery({
    queryKey: reportKeys.section("summary", params),
    queryFn: () => reportsApi.summary(params),
    enabled: !!from && !!to && from <= to,
  });
  const finance = useQuery({
    queryKey: reportKeys.section("finance", params),
    queryFn: () => reportsApi.finance(params),
    enabled: !!from && !!to && from <= to,
  });
  const engagement = useQuery({
    queryKey: reportKeys.section("engagement", params),
    queryFn: () => reportsApi.engagement(params),
    enabled: !!from && !!to && from <= to,
  });
  const capacity = useQuery({
    queryKey: reportKeys.section("capacity", params),
    queryFn: () => reportsApi.capacity(params),
    enabled: !!from && !!to && from <= to,
  });
  const instructors = useQuery({
      queryKey: classKeys.instructors,
      queryFn: classesApi.instructors,
    }),
    rooms = useQuery({ queryKey: classKeys.rooms, queryFn: classesApi.rooms }),
    classes = useQuery({
      queryKey: classKeys.list("report-options"),
      queryFn: () =>
        classesApi.list(
          new URLSearchParams({ pageSize: "100", status: "Active" }),
        ),
    });
  const choosePreset = (value: Preset) => {
    setPreset(value);
    if (value !== "custom") {
      const range = rangeFor(value);
      setFrom(range.from);
      setTo(range.to);
    }
  };
  return (
    <div className="space-y-6">
      <section className="rounded-xl border bg-white p-4 shadow-sm">
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
          <Filter label="Tarih aralığı">
            <select
              className="control"
              value={preset}
              onChange={(event) => choosePreset(event.target.value as Preset)}
            >
              <option value="thisMonth">Bu ay</option>
              <option value="lastMonth">Geçen ay</option>
              <option value="last30">Son 30 gün</option>
              <option value="last3Months">Son 3 ay</option>
              <option value="thisYear">Bu yıl</option>
              <option value="custom">Özel tarih aralığı</option>
            </select>
          </Filter>
          {preset === "custom" && (
            <>
              <Filter label="Başlangıç">
                <Input
                  type="date"
                  value={from}
                  max={to}
                  onChange={(event) => setFrom(event.target.value)}
                />
              </Filter>
              <Filter label="Bitiş">
                <Input
                  type="date"
                  value={to}
                  min={from}
                  max={today()}
                  onChange={(event) => setTo(event.target.value)}
                />
              </Filter>
            </>
          )}
          <Filter label="Eğitmen">
            <select
              className="control"
              value={instructorId}
              onChange={(event) => setInstructorId(event.target.value)}
            >
              <option value="">Tümü</option>
              {instructors.data?.map((x) => (
                <option key={x.id} value={x.id}>
                  {x.fullName}
                </option>
              ))}
            </select>
          </Filter>
          <Filter label="Sınıf">
            <select
              className="control"
              value={classId}
              onChange={(event) => setClassId(event.target.value)}
            >
              <option value="">Tümü</option>
              {classes.data?.items.map((x) => (
                <option key={x.id} value={x.id}>
                  {x.name}
                </option>
              ))}
            </select>
          </Filter>
          <Filter label="Stüdyo">
            <select
              className="control"
              value={roomId}
              onChange={(event) => setRoomId(event.target.value)}
            >
              <option value="">Tümü</option>
              {rooms.data?.map((x) => (
                <option key={x.id} value={x.id}>
                  {x.name}
                </option>
              ))}
            </select>
          </Filter>
        </div>
        {from > to && (
          <p role="alert" className="mt-3 text-sm text-red-600">
            Başlangıç tarihi bitiş tarihinden sonra olamaz.
          </p>
        )}
      </section>
      {summary.isLoading ? (
        <KpiSkeleton />
      ) : summary.isError ? (
        <SectionError error={summary.error} retry={() => summary.refetch()} />
      ) : (
        <div className="grid grid-cols-2 gap-3 lg:grid-cols-3 xl:grid-cols-6">
          <Kpi
            label="Aktif öğrenci"
            metric={summary.data!.activeStudents}
            href="/students"
          />
          <Kpi
            label="Yeni öğrenci"
            metric={summary.data!.newStudents}
            href="/students"
          />
          <Kpi
            label="Aktif üyelik"
            metric={summary.data!.activeMemberships}
            href="/memberships"
          />
          <Kpi
            label="Toplam tahsilat"
            metric={summary.data!.totalRevenue}
            href="/payments"
            moneyValue
          />
          <Kpi
            label="Açık bakiye"
            metric={summary.data!.outstandingBalance}
            href="/balances"
            moneyValue
          />
          <Kpi
            label="Ort. devam"
            metric={summary.data!.attendanceRate}
            href="/attendance"
            suffix="%"
          />
        </div>
      )}
      <div className="grid gap-6 lg:grid-cols-2">
        <FinanceSection query={finance} />
        <EngagementSection query={engagement} />
      </div>
      <CapacitySection query={capacity} />
    </div>
  );
}

function FinanceSection({
  query,
}: {
  query: ReturnType<typeof useQuery<ReportFinance>>;
}) {
  if (query.isLoading) return <SectionSkeleton />;
  if (query.isError)
    return <SectionError error={query.error} retry={() => query.refetch()} />;
  const data = query.data!;
  return (
    <div className="space-y-6">
      <Panel
        title="Tahsilat raporu"
        action={
          <CsvButton
            name="tahsilat-raporu"
            headers={["Yöntem", "Adet", "Tutar"]}
            rows={data.revenue.methods.map((x) => [
              methodLabels[x.method] ?? x.method,
              x.count,
              x.amount,
            ])}
          />
        }
      >
        <div className="grid grid-cols-3 gap-3">
          <Mini label="Toplam" value={money(data.revenue.total)} />
          <Mini label="Ödeme adedi" value={String(data.revenue.paymentCount)} />
          <Mini label="Ortalama" value={money(data.revenue.averagePayment)} />
        </div>
        <TrendChart
          data={data.revenue.trend}
          moneyValues
          empty="Seçili dönemde tahsilat yok."
        />
        <Distribution
          data={data.revenue.methods.map((x) => ({
            label: methodLabels[x.method] ?? x.method,
            value: x.amount,
          }))}
          moneyValues
        />
        {data.revenue.peakLabel && (
          <p className="mt-4 text-xs text-zinc-500">
            En yüksek dönem:{" "}
            <b>
              {data.revenue.peakLabel} · {money(data.revenue.peakAmount ?? 0)}
            </b>
          </p>
        )}
      </Panel>
      <Panel
        title="Borç ve faturalar"
        action={<LinkAction href="/balances" label="Borçlara git" />}
      >
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
          <Mini
            label="Faturalanan"
            value={money(data.balances.totalInvoiced)}
          />
          <Mini label="Ödenen" value={money(data.balances.totalPaid)} />
          <Mini label="Açık" value={money(data.balances.outstandingBalance)} />
          <Mini label="Gecikmiş" value={money(data.balances.overdueBalance)} />
          <Mini
            label="Gecikmiş fatura"
            value={String(data.balances.overdueInvoiceCount)}
          />
        </div>
        <Distribution
          data={data.balances.statuses.map((x) => ({
            label: statusLabels[x.status] ?? x.status,
            value: x.count,
          }))}
        />
        <ReportList
          title="En yüksek açık bakiyeler"
          action={
            <CsvButton
              name="acik-bakiyeler"
              headers={["Öğrenci", "Faturalanan", "Ödenen", "Açık"]}
              rows={data.balances.topDebtors.map((x) => [
                x.studentName,
                x.invoiced,
                x.paid,
                x.outstanding,
              ])}
            />
          }
          empty="Açık bakiyesi olan öğrenci yok."
        >
          {data.balances.topDebtors.map((x) => (
            <Link
              key={x.studentId}
              href={studentDetailHref(x.studentId)}
              className="flex items-center justify-between gap-3 border-b py-2 text-sm last:border-0"
            >
              <span>{x.studentName}</span>
              <b className="text-rose-700">{money(x.outstanding)}</b>
            </Link>
          ))}
        </ReportList>
      </Panel>
    </div>
  );
}

function EngagementSection({
  query,
}: {
  query: ReturnType<typeof useQuery<ReportEngagement>>;
}) {
  if (query.isLoading) return <SectionSkeleton />;
  if (query.isError)
    return <SectionError error={query.error} retry={() => query.refetch()} />;
  const { students, attendance } = query.data!;
  const low = [...attendance.classes]
      .sort((a, b) => a.rate - b.rate)
      .slice(0, 3),
    high = [...attendance.classes].sort((a, b) => b.rate - a.rate).slice(0, 3);
  return (
    <div className="space-y-6">
      <Panel
        title="Devam ve yoklama"
        action={
          <LinkAction
            href="/attendance"
            label={`${attendance.missingSessions} eksik yoklama`}
          />
        }
      >
        <div className="grid grid-cols-3 gap-3">
          <Mini label="Toplam" value={String(attendance.total)} />
          <Mini label="Devam" value={`%${attendance.rate}`} />
          <Mini label="Gelmedi" value={String(attendance.absent)} />
          <Mini label="Mazeretli" value={String(attendance.excused)} />
          <Mini label="Geç" value={String(attendance.late)} />
          <Mini label="Telafi" value={String(attendance.makeUp)} />
        </div>
        <TrendChart
          data={attendance.trend}
          suffix="%"
          empty="Seçili dönemde yoklama kaydı yok."
        />
        <div className="mt-5 grid gap-4 sm:grid-cols-2">
          <ClassRates title="En yüksek devam" items={high} />
          <ClassRates title="En düşük devam" items={low} />
        </div>
      </Panel>
      <Panel
        title="Öğrenci raporu"
        action={<LinkAction href="/students" label="Öğrencilere git" />}
      >
        <div className="grid grid-cols-3 gap-3">
          <Mini label="Toplam" value={String(students.total)} />
          <Mini label="Aktif" value={String(students.active)} />
          <Mini label="Yeni kayıt" value={String(students.newStudents)} />
        </div>
        <Distribution
          data={students.statuses.map((x) => ({
            label: statusLabels[x.label] ?? x.label,
            value: x.value,
          }))}
        />
        <TrendChart
          data={students.newStudentTrend}
          empty="Seçili dönemde yeni öğrenci yok."
        />
      </Panel>
    </div>
  );
}

function CapacitySection({
  query,
}: {
  query: ReturnType<typeof useQuery<ReportCapacity>>;
}) {
  if (query.isLoading) return <SectionSkeleton />;
  if (query.isError)
    return <SectionError error={query.error} retry={() => query.refetch()} />;
  const data = query.data!,
    sorted = [...data.classes].sort(
      (a, b) => b.occupancyRate - a.occupancyRate,
    );
  return (
    <div className="space-y-6">
      <Panel
        title="Sınıf dolulukları"
        action={
          <CsvButton
            name="sinif-doluluklari"
            headers={[
              "Sınıf",
              "Eğitmen",
              "Stüdyo",
              "Kapasite",
              "Aktif öğrenci",
              "Doluluk %",
            ]}
            rows={sorted.map((x) => [
              x.className,
              x.instructorName,
              x.roomName,
              x.capacity,
              x.activeStudents,
              x.occupancyRate,
            ])}
          />
        }
      >
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {sorted.length === 0 ? (
            <Empty text="Aktif sınıf bulunmuyor." />
          ) : (
            sorted.map((x, index) => (
              <Link
                key={x.classId}
                href={classDetailHref(x.classId)}
                className="rounded-lg border p-4 hover:border-[#718360]"
              >
                <div className="flex items-start justify-between gap-2">
                  <div>
                    <p className="font-medium">{x.className}</p>
                    <p className="mt-1 text-xs text-zinc-500">
                      {x.instructorName} · {x.roomName}
                    </p>
                  </div>
                  <OccupancyBadge rate={x.occupancyRate} />
                </div>
                <div className="mt-3 h-2 overflow-hidden rounded-full bg-zinc-100">
                  <div
                    className="h-full rounded-full bg-[#718360]"
                    style={{ width: `${Math.min(100, x.occupancyRate)}%` }}
                  />
                </div>
                <p className="mt-2 text-xs text-zinc-500">
                  {x.activeStudents}/{x.capacity} öğrenci
                  {index === 0
                    ? " · En dolu"
                    : index === sorted.length - 1
                      ? " · En boş"
                      : ""}
                </p>
              </Link>
            ))
          )}
        </div>
      </Panel>
      <Panel
        title="Eğitmen operasyon özeti"
        action={
          <CsvButton
            name="egitmen-raporu"
            headers={[
              "Eğitmen",
              "Aktif sınıf",
              "Öğrenci",
              "Ders",
              "Ort. doluluk %",
              "Devam %",
            ]}
            rows={data.instructors.map((x) => [
              x.instructorName,
              x.activeClasses,
              x.totalStudents,
              x.sessions,
              x.averageOccupancy,
              x.attendanceRate,
            ])}
          />
        }
      >
        <CardGrid empty="Seçili filtrelerde eğitmen verisi yok.">
          {data.instructors.map((x) => (
            <div key={x.instructorId} className="rounded-lg border p-4">
              <p className="font-medium">{x.instructorName}</p>
              <div className="mt-3 grid grid-cols-2 gap-2 text-xs text-zinc-500">
                <span>{x.activeClasses} aktif sınıf</span>
                <span>{x.totalStudents} öğrenci</span>
                <span>{x.sessions} ders</span>
                <span>%{x.attendanceRate} devam</span>
                <span className="col-span-2">
                  %{x.averageOccupancy} ort. doluluk
                </span>
              </div>
            </div>
          ))}
        </CardGrid>
      </Panel>
      <Panel
        title="Üyelik raporu"
        action={
          <CsvButton
            name="uyelik-planlari"
            headers={[
              "Plan",
              "Aktif öğrenci",
              "Faturalanan",
              "Ort. fiyat",
              "İndirimli",
            ]}
            rows={data.memberships.plans.map((x) => [
              x.planName,
              x.activeStudents,
              x.totalInvoiced,
              x.averagePrice,
              x.discountedMemberships,
            ])}
          />
        }
      >
        <div className="grid grid-cols-4 gap-3">
          <Mini label="Aktif" value={String(data.memberships.active)} />
          <Mini label="Donmuş" value={String(data.memberships.frozen)} />
          <Mini
            label="Süresi dolmuş"
            value={String(data.memberships.expired)}
          />
          <Mini label="İptal" value={String(data.memberships.cancelled)} />
        </div>
        <ReportList title="Plan dağılımı" empty="Üyelik planı verisi yok.">
          {data.memberships.plans.map((x) => (
            <div
              key={x.planId}
              className="grid grid-cols-2 gap-2 border-b py-3 text-sm last:border-0 sm:grid-cols-4"
            >
              <b>{x.planName}</b>
              <span>{x.activeStudents} aktif</span>
              <span>{money(x.totalInvoiced)}</span>
              <span>{x.discountedMemberships} indirimli</span>
            </div>
          ))}
        </ReportList>
        <ReportList
          title="30 gün içinde bitecek üyelikler"
          empty="Yakında bitecek üyelik yok."
        >
          {data.memberships.expiring.map((x) => (
            <Link
              key={x.membershipId}
              href={studentDetailHref(x.studentId)}
              className="flex items-center justify-between gap-3 border-b py-2 text-sm last:border-0"
            >
              <span>
                {x.studentName} · {x.planName}
              </span>
              <span>
                {x.daysRemaining <= 7
                  ? "7 gün"
                  : x.daysRemaining <= 14
                    ? "14 gün"
                    : "30 gün"}{" "}
                içinde
              </span>
            </Link>
          ))}
        </ReportList>
      </Panel>
    </div>
  );
}

function Kpi({
  label,
  metric,
  href,
  moneyValue = false,
  suffix = "",
}: {
  label: string;
  metric: ReportMetric;
  href: string;
  moneyValue?: boolean;
  suffix?: string;
}) {
  return (
    <Link
      href={href}
      className="rounded-xl border bg-white p-4 shadow-sm hover:border-[#718360]"
    >
      <p className="text-xs font-medium uppercase text-zinc-400">{label}</p>
      <p className="mt-2 text-xl font-semibold">
        {moneyValue ? money(metric.value) : metric.value + suffix}
      </p>
      {metric.trendPercent !== null && (
        <p
          className={`mt-1 text-xs ${metric.trendPercent >= 0 ? "text-emerald-700" : "text-rose-700"}`}
        >
          {metric.trendPercent >= 0 ? "+" : ""}
          {metric.trendPercent}% önceki döneme göre
        </p>
      )}
    </Link>
  );
}
function Panel({
  title,
  action,
  children,
}: {
  title: string;
  action?: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <section className="rounded-xl border bg-white p-5 shadow-sm">
      <div className="mb-5 flex items-center justify-between gap-3">
        <h2 className="font-semibold">{title}</h2>
        {action}
      </div>
      {children}
    </section>
  );
}
function TrendChart({
  data,
  moneyValues = false,
  suffix = "",
  empty,
}: {
  data: ReportPoint[];
  moneyValues?: boolean;
  suffix?: string;
  empty: string;
}) {
  if (!data.some((x) => x.value > 0)) return <Empty text={empty} />;
  const max = Math.max(...data.map((x) => x.value), 1);
  return (
    <div className="mt-5">
      <div className="flex h-36 items-end gap-1 rounded-lg bg-zinc-50 p-3">
        {data.map((x, index) => (
          <div
            key={`${x.label}-${index}`}
            title={`${x.label}: ${moneyValues ? money(x.value) : x.value + suffix}`}
            className="group flex h-full min-w-0 flex-1 items-end"
          >
            <div
              className="w-full rounded-t bg-[#718360] group-hover:bg-[#526743]"
              style={{ height: `${Math.max(2, (100 * x.value) / max)}%` }}
            />
          </div>
        ))}
      </div>
      <div className="mt-1 flex justify-between text-[10px] text-zinc-400">
        <span>{data[0]?.label}</span>
        <span>{data.at(-1)?.label}</span>
      </div>
    </div>
  );
}
function Distribution({
  data,
  moneyValues = false,
}: {
  data: { label: string; value: number }[];
  moneyValues?: boolean;
}) {
  const max = Math.max(...data.map((x) => x.value), 1);
  return (
    <div className="mt-5 space-y-3">
      {data.map((x) => (
        <div key={x.label}>
          <div className="mb-1 flex justify-between text-xs">
            <span>{x.label}</span>
            <b>{moneyValues ? money(x.value) : x.value}</b>
          </div>
          <div className="h-1.5 rounded-full bg-zinc-100">
            <div
              className="h-full rounded-full bg-[#8a997d]"
              style={{ width: `${(100 * x.value) / max}%` }}
            />
          </div>
        </div>
      ))}
    </div>
  );
}
function ClassRates({
  title,
  items,
}: {
  title: string;
  items: ReportEngagement["attendance"]["classes"];
}) {
  return (
    <div>
      <p className="text-xs font-semibold uppercase text-zinc-400">{title}</p>
      <div className="mt-2 space-y-2">
        {items.length === 0 ? (
          <p className="text-xs text-zinc-500">Veri yok.</p>
        ) : (
          items.map((x) => (
            <Link
              key={x.classId}
              href={classDetailHref(x.classId)}
              className="flex justify-between text-sm hover:underline"
            >
              <span>{x.className}</span>
              <b>%{x.rate}</b>
            </Link>
          ))
        )}
      </div>
    </div>
  );
}
function ReportList({
  title,
  action,
  empty,
  children,
}: {
  title: string;
  action?: React.ReactNode;
  empty: string;
  children: React.ReactNode[];
}) {
  return (
    <div className="mt-5 border-t pt-4">
      <div className="mb-2 flex items-center justify-between gap-2">
        <h3 className="text-sm font-semibold">{title}</h3>
        {action}
      </div>
      {children.length ? (
        children
      ) : (
        <p className="text-sm text-zinc-500">{empty}</p>
      )}
    </div>
  );
}
function CsvButton({
  name,
  headers,
  rows,
}: {
  name: string;
  headers: string[];
  rows: (string | number)[][];
}) {
  return (
    <Button
      size="sm"
      variant="outline"
      disabled={!rows.length}
      onClick={() => downloadCsv(name, headers, rows)}
    >
      <Download />
      CSV
    </Button>
  );
}
function downloadCsv(
  name: string,
  headers: string[],
  rows: (string | number)[][],
) {
  const escape = (value: string | number) =>
      `"${String(value).replaceAll('"', '""')}"`,
    content =
      "\uFEFF" +
      [headers, ...rows].map((row) => row.map(escape).join(";")).join("\r\n"),
    url = URL.createObjectURL(
      new Blob([content], { type: "text/csv;charset=utf-8" }),
    ),
    link = document.createElement("a");
  link.href = url;
  link.download = `${name}.csv`;
  link.click();
  URL.revokeObjectURL(url);
}
function Filter({
  label,
  children,
}: {
  label: string;
  children: React.ReactNode;
}) {
  return (
    <label className="text-xs font-medium text-zinc-500">
      <span className="mb-1.5 block">{label}</span>
      {children}
    </label>
  );
}
function Mini({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg bg-zinc-50 p-3">
      <p className="text-xs text-zinc-500">{label}</p>
      <p className="mt-1 font-semibold">{value}</p>
    </div>
  );
}
function LinkAction({ href, label }: { href: string; label: string }) {
  return (
    <Link
      href={href}
      className="inline-flex items-center gap-1 text-xs font-medium text-[#526743] hover:underline"
    >
      {label}
      <ArrowRight className="size-3" />
    </Link>
  );
}
function OccupancyBadge({ rate }: { rate: number }) {
  const label =
      rate >= 100
        ? "Dolu"
        : rate >= 90
          ? "Dolmak üzere"
          : rate >= 60
            ? "İyi"
            : "Normal",
    color =
      rate >= 100
        ? "bg-rose-50 text-rose-700"
        : rate >= 90
          ? "bg-amber-50 text-amber-700"
          : rate >= 60
            ? "bg-emerald-50 text-emerald-700"
            : "bg-zinc-100 text-zinc-600";
  return (
    <span className={`rounded-md px-2 py-1 text-xs ${color}`}>
      %{rate} · {label}
    </span>
  );
}
function CardGrid({
  empty,
  children,
}: {
  empty: string;
  children: React.ReactNode[];
}) {
  return children.length ? (
    <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">{children}</div>
  ) : (
    <Empty text={empty} />
  );
}
function Empty({ text }: { text: string }) {
  return (
    <div className="mt-4 rounded-lg border border-dashed bg-zinc-50 p-5 text-center text-sm text-zinc-500">
      {text}
    </div>
  );
}
function KpiSkeleton() {
  return (
    <div className="grid grid-cols-2 gap-3 xl:grid-cols-6">
      {Array.from({ length: 6 }, (_, i) => (
        <div key={i} className="h-24 animate-pulse rounded-xl bg-zinc-100" />
      ))}
    </div>
  );
}
function SectionSkeleton() {
  return (
    <div className="space-y-3 rounded-xl border bg-white p-5">
      {Array.from({ length: 4 }, (_, i) => (
        <div key={i} className="h-16 animate-pulse rounded-lg bg-zinc-100" />
      ))}
    </div>
  );
}
function SectionError({ error, retry }: { error: unknown; retry: () => void }) {
  return (
    <div
      role="alert"
      className="rounded-xl border border-red-200 bg-red-50 p-5 text-sm text-red-700"
    >
      <p>
        {formErrorMessage(
          error,
          "Rapor bölümü yüklenirken beklenmeyen bir hata oluştu.",
        )}
      </p>
      <Button size="sm" variant="outline" className="mt-3" onClick={retry}>
        <RefreshCw />
        Tekrar dene
      </Button>
    </div>
  );
}
