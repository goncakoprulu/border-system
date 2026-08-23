"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  Banknote,
  CalendarDays,
  ChevronRight,
  CircleAlert,
  ClipboardCheck,
  LoaderCircle,
  Plus,
  ReceiptText,
  Search,
  Settings,
  ShieldCheck,
  UserCog,
  Users,
  WalletCards,
} from "lucide-react";
import Link from "next/link";
import { ReactNode, useDeferredValue, useMemo, useState } from "react";
import { toast } from "sonner";
import { InstructorManagementDialog } from "@/components/classes/instructor-management-dialog";
import { RoomManagementDialog } from "@/components/classes/room-management-dialog";
import { AttendanceSection } from "@/components/operations/attendance-section";
import { BalancesSection } from "@/components/operations/balances-section";
import { ReportsSection } from "@/components/operations/reports-section";
import {
  MembershipDialog,
  PaymentDialog,
} from "@/components/operations/finance-dialogs";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { classesApi, classKeys, dayLabels, displayTime } from "@/lib/classes";
import { formErrorMessage } from "@/lib/form-errors";
import {
  operationKeys,
  operationsApi,
  Membership,
  Payment,
  UserRecord,
} from "@/lib/operations";
import { classDetailHref, studentDetailHref } from "@/lib/routes";
import { normalizeScheduleDay, scheduleDayLabel } from "@/lib/schedule-days";

type Section =
  | "schedule"
  | "attendance"
  | "memberships"
  | "payments"
  | "balances"
  | "reports"
  | "instructors"
  | "users"
  | "settings";
const sectionMeta: Record<
  Section,
  {
    eyebrow: string;
    title: string;
    description: string;
    icon: typeof CalendarDays;
  }
> = {
  schedule: {
    eyebrow: "Haftalık görünüm",
    title: "Program",
    description: "Dersleri gün, salon ve eğitmen bazında tek bakışta izleyin.",
    icon: CalendarDays,
  },
  attendance: {
    eyebrow: "Günlük operasyon",
    title: "Yoklama",
    description: "Herkesi geldi işaretleyip yalnızca istisnaları değiştirin.",
    icon: ClipboardCheck,
  },
  memberships: {
    eyebrow: "Öğrenci finansı",
    title: "Üyelikler",
    description:
      "Aktif ve geçmiş üyelikleri yönetin, yeni üyeliği hızlıca atayın.",
    icon: WalletCards,
  },
  payments: {
    eyebrow: "Günlük tahsilat",
    title: "Ödemeler",
    description: "Öğrencinin açık borcunu görün ve tahsilatı kaydedin.",
    icon: Banknote,
  },
  balances: {
    eyebrow: "Finansal görünüm",
    title: "Borç Bakiyeleri",
    description: "Açık bakiyeyi ve tahsilat durumunu öğrenci bazında izleyin.",
    icon: ReceiptText,
  },
  reports: {
    eyebrow: "Yönetim özeti",
    title: "Raporlar",
    description:
      "Temel göstergeleri sade ve karşılaştırılabilir biçimde takip edin.",
    icon: ShieldCheck,
  },
  instructors: {
    eyebrow: "Ekip yönetimi",
    title: "Eğitmenler",
    description: "İletişim, aktif ders ve haftalık programı yönetin.",
    icon: UserCog,
  },
  users: {
    eyebrow: "Erişim yönetimi",
    title: "Kullanıcılar",
    description: "Identity kullanıcılarının rollerini ve durumunu yönetin.",
    icon: Users,
  },
  settings: {
    eyebrow: "Operasyon ayarları",
    title: "Ayarlar",
    description: "Salonları, üyelik planlarını ve fiyatları yönetin.",
    icon: Settings,
  },
};
const money = (v: number) =>
  new Intl.NumberFormat("tr-TR", { style: "currency", currency: "TRY" }).format(
    v,
  );
const date = (v: string | null) =>
  v
    ? new Intl.DateTimeFormat("tr-TR", { dateStyle: "medium" }).format(
        new Date(v),
      )
    : "—";
export function ManagementSection({ section }: { section: Section }) {
  const m = sectionMeta[section],
    Icon = m.icon;
  return (
    <div className="mx-auto max-w-7xl">
      <header className="flex items-start gap-4">
        <div className="mt-1 hidden size-11 place-items-center rounded-xl bg-[#e9ede4] text-[#526743] sm:grid">
          <Icon size={21} />
        </div>
        <div>
          <p className="text-sm font-medium text-[#61734f]">{m.eyebrow}</p>
          <h1 className="mt-1 text-2xl font-semibold sm:text-3xl">{m.title}</h1>
          <p className="mt-2 text-sm text-zinc-500">{m.description}</p>
        </div>
      </header>
      <div className="mt-7">
        {section === "schedule" ? (
          <Schedule />
        ) : section === "attendance" ? (
          <AttendanceSection />
        ) : section === "memberships" ? (
          <Memberships />
        ) : section === "payments" ? (
          <Payments />
        ) : section === "balances" ? (
          <BalancesSection />
        ) : section === "reports" ? (
          <ReportsSection />
        ) : section === "instructors" ? (
          <Instructors />
        ) : section === "users" ? (
          <UsersPanel />
        ) : (
          <SettingsPanel />
        )}
      </div>
    </div>
  );
}

function Schedule() {
  const [day, setDay] = useState(""),
    [room, setRoom] = useState(""),
    [instructor, setInstructor] = useState(""),
    [studioClass, setStudioClass] = useState("");
  const params = useMemo(() => {
    const p = new URLSearchParams();
    if (day) p.set("day", day);
    if (room) p.set("roomId", room);
    if (instructor) p.set("instructorId", instructor);
    if (studioClass) p.set("classId", studioClass);
    return p.toString();
  }, [day, room, instructor, studioClass]);
  const q = useQuery({
    queryKey: operationKeys.section("schedule", params),
    queryFn: () => operationsApi.schedule(params),
  });
  const rooms = useQuery({
      queryKey: classKeys.rooms,
      queryFn: classesApi.rooms,
    }),
    instructors = useQuery({
      queryKey: classKeys.instructors,
      queryFn: classesApi.instructors,
    }),
    classes = useQuery({
      queryKey: classKeys.list("schedule-options"),
      queryFn: () =>
        classesApi.list(
          new URLSearchParams({ pageSize: "100", status: "Active" }),
        ),
    });
  const days = [1, 2, 3, 4, 5, 6, 0];
  return (
    <Panel>
      <Filters>
        <Select
          label="Gün"
          value={day}
          onChange={setDay}
          options={days.map((x) => ({ value: String(x), label: dayLabels[x] }))}
        />
        <Select
          label="Salon"
          value={room}
          onChange={setRoom}
          options={(rooms.data ?? []).map((x) => ({
            value: x.id,
            label: x.name,
          }))}
        />
        <Select
          label="Eğitmen"
          value={instructor}
          onChange={setInstructor}
          options={(instructors.data ?? []).map((x) => ({
            value: x.id,
            label: x.fullName,
          }))}
        />
        <Select
          label="Sınıf"
          value={studioClass}
          onChange={setStudioClass}
          options={(classes.data?.items ?? []).map((x) => ({
            value: x.id,
            label: x.name,
          }))}
        />
      </Filters>
      {q.isLoading ? (
        <Loading />
      ) : q.isError ? (
        <ErrorState error={q.error} />
      ) : q.data?.length === 0 ? (
        <Empty
          icon={<CalendarDays />}
          title="Program kaydı bulunamadı"
          detail="Seçili filtrelerde aktif ders yok."
        />
      ) : (
        <>
          <div className="hidden grid-cols-7 divide-x lg:grid">
            {days.map((d) => (
              <div key={d}>
                <div className="border-b bg-zinc-50 px-2 py-3 text-center text-xs font-semibold uppercase text-zinc-500">
                  {dayLabels[d]}
                </div>
                <div className="min-h-96 space-y-2 p-2">
                  {q.data
                    ?.filter((x) => normalizeScheduleDay(x.dayOfWeek) === d)
                    .map((x) => (
                      <LessonCard
                        key={`${x.classId}-${x.startTime}`}
                        item={x}
                      />
                    ))}
                </div>
              </div>
            ))}
          </div>
          <div className="divide-y lg:hidden">
            {days
              .filter((d) => !day || String(d) === day)
              .map((d) => (
                <div key={d} className="p-4">
                  <h2 className="mb-3 text-sm font-semibold">{dayLabels[d]}</h2>
                  <div className="space-y-2">
                    {q.data
                      ?.filter((x) => normalizeScheduleDay(x.dayOfWeek) === d)
                      .map((x) => (
                        <LessonCard
                          key={`${x.classId}-${x.startTime}`}
                          item={x}
                        />
                      ))}
                  </div>
                </div>
              ))}
          </div>
          <UnknownScheduleItems items={q.data ?? []} />
        </>
      )}
    </Panel>
  );
}
function LessonCard({
  item: x,
}: {
  item: Awaited<ReturnType<typeof operationsApi.schedule>>[number];
}) {
  return (
    <Link
      href={classDetailHref(x.classId)}
      className="block rounded-lg border-l-4 border-l-[#718360] bg-[#f5f7f2] p-3 hover:bg-[#edf1e9]"
    >
      <div className="flex justify-between gap-2">
        <p className="truncate text-sm font-semibold">{x.className}</p>
        <span className="text-xs font-medium text-[#526743]">
          {displayTime(x.startTime)}
        </span>
      </div>
      <p className="mt-1 truncate text-xs text-zinc-600">{x.instructorName}</p>
      <p className="mt-1 text-xs text-zinc-400">
        {x.roomName} · {x.level ?? "Seviye yok"} · {displayTime(x.endTime)}
      </p>
    </Link>
  );
}
function UnknownScheduleItems({
  items,
}: {
  items: Awaited<ReturnType<typeof operationsApi.schedule>>;
}) {
  const unknown = items.filter(
    (x) => normalizeScheduleDay(x.dayOfWeek) === null,
  );
  if (!unknown.length) return null;
  return (
    <div className="border-t border-amber-200 bg-amber-50 p-4">
      <h2 className="mb-3 text-sm font-semibold text-amber-800">
        Bilinmeyen gün
      </h2>
      <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
        {unknown.map((x) => (
          <LessonCard key={`${x.classId}-${x.startTime}`} item={x} />
        ))}
      </div>
    </div>
  );
}

function Memberships() {
  const qc = useQueryClient(),
    [search, setSearch] = useState(""),
    deferred = useDeferredValue(search),
    [status, setStatus] = useState(""),
    [open, setOpen] = useState(false);
  const p = new URLSearchParams();
  if (deferred) p.set("search", deferred);
  if (status) p.set("status", status);
  const q = useQuery({
    queryKey: operationKeys.section("memberships", p.toString()),
    queryFn: () => operationsApi.memberships(p.toString()),
  });
  return (
    <>
      <Toolbar
        search={search}
        setSearch={setSearch}
        placeholder="Öğrenci ara..."
      >
        <Select
          label="Durum"
          value={status}
          onChange={setStatus}
          options={[
            ["Active", "Aktif"],
            ["Frozen", "Donmuş"],
            ["Expired", "Süresi dolmuş"],
            ["Cancelled", "İptal"],
          ].map(([value, label]) => ({ value, label }))}
        />
        <Button onClick={() => setOpen(true)}>
          <Plus />
          Yeni üyelik
        </Button>
      </Toolbar>
      <Panel>
        {q.isLoading ? (
          <Loading />
        ) : q.isError ? (
          <ErrorState error={q.error} />
        ) : q.data?.length === 0 ? (
          <Empty
            icon={<WalletCards />}
            title="Üyelik bulunamadı"
            detail="Filtreyi değiştirin veya ilk üyeliği atayın."
          />
        ) : (
          <ResponsiveTable
            headers={[
              "Öğrenci",
              "Üyelik",
              "Başlangıç",
              "Bitiş",
              "Durum",
              "Ücret",
              "Kalan",
            ]}
          >
            {q.data?.map((x) => (
              <MembershipRow key={x.id} item={x} />
            ))}
          </ResponsiveTable>
        )}
      </Panel>
      <MembershipDialog
        open={open}
        onOpenChange={setOpen}
        onSaved={() => qc.invalidateQueries({ queryKey: ["operations"] })}
      />
    </>
  );
}
function MembershipRow({ item: x }: { item: Membership }) {
  return (
    <TableRow>
      <TableCell>
        <Link
          className="font-medium hover:underline"
          href={studentDetailHref(x.studentId)}
        >
          {x.studentName}
        </Link>
      </TableCell>
      <TableCell>{x.planName}</TableCell>
      <TableCell>{date(x.startDate)}</TableCell>
      <TableCell>{date(x.endDate)}</TableCell>
      <TableCell>
        <Tag>{x.status}</Tag>
      </TableCell>
      <TableCell>{money(x.price)}</TableCell>
      <TableCell>{x.remainingLessons ?? "—"}</TableCell>
    </TableRow>
  );
}
function Payments() {
  const qc = useQueryClient(),
    [search, setSearch] = useState(""),
    deferred = useDeferredValue(search),
    [open, setOpen] = useState(false);
  const q = useQuery({
    queryKey: operationKeys.section("payments", deferred),
    queryFn: () =>
      operationsApi.payments(
        deferred ? `search=${encodeURIComponent(deferred)}` : "",
      ),
  });
  return (
    <>
      <Toolbar
        search={search}
        setSearch={setSearch}
        placeholder="Öğrenci ara..."
      >
        <Button onClick={() => setOpen(true)}>
          <Plus />
          Ödeme al
        </Button>
      </Toolbar>
      <Panel>
        {q.isLoading ? (
          <Loading />
        ) : q.isError ? (
          <ErrorState error={q.error} />
        ) : q.data?.length === 0 ? (
          <Empty
            icon={<Banknote />}
            title="Ödeme bulunamadı"
            detail="Aramayı değiştirin veya ilk tahsilatı kaydedin."
          />
        ) : (
          <ResponsiveTable
            headers={[
              "Öğrenci",
              "Tutar",
              "Tarih",
              "Yöntem",
              "İlgili borç",
              "Açıklama",
            ]}
          >
            {q.data?.map((x) => (
              <PaymentRow key={x.id} item={x} />
            ))}
          </ResponsiveTable>
        )}
      </Panel>
      <PaymentDialog
        open={open}
        onOpenChange={setOpen}
        onSaved={() => qc.invalidateQueries({ queryKey: ["operations"] })}
      />
    </>
  );
}
function PaymentRow({ item: x }: { item: Payment }) {
  return (
    <TableRow>
      <TableCell>
        <Link
          className="font-medium hover:underline"
          href={studentDetailHref(x.studentId)}
        >
          {x.studentName}
        </Link>
      </TableCell>
      <TableCell className="font-semibold">{money(x.amount)}</TableCell>
      <TableCell>{date(x.paymentDate)}</TableCell>
      <TableCell>
        {
          (
            {
              Cash: "Nakit",
              CreditCard: "Kredi kartı",
              BankTransfer: "Havale",
              Other: "Diğer",
            } as Record<string, string>
          )[x.paymentMethod]
        }
      </TableCell>
      <TableCell>{x.invoiceDescription ?? "Serbest ödeme"}</TableCell>
      <TableCell>{x.notes ?? "—"}</TableCell>
    </TableRow>
  );
}
function Instructors() {
  const qc = useQueryClient(),
    [manage, setManage] = useState(false),
    [selected, setSelected] = useState("");
  const list = useQuery({
      queryKey: [...classKeys.instructors, "records"],
      queryFn: classesApi.instructorRecords,
    }),
    detail = useQuery({
      queryKey: operationKeys.section("instructor", selected),
      queryFn: () => operationsApi.instructor(selected),
      enabled: !!selected,
    });
  return (
    <>
      <div className="mb-4 flex justify-end">
        <Button onClick={() => setManage(true)}>
          <UserCog />
          Eğitmenleri yönet
        </Button>
      </div>
      <Panel>
        {list.isLoading ? (
          <Loading />
        ) : list.isError ? (
          <ErrorState error={list.error} />
        ) : (
          <div className="divide-y">
            {list.data?.map((x) => (
              <button
                key={x.id}
                onClick={() => setSelected(x.id)}
                className="flex w-full items-center gap-3 p-4 text-left hover:bg-zinc-50"
              >
                <div className="grid size-10 place-items-center rounded-full bg-[#edf1e9] font-semibold text-[#526743]">
                  {x.firstName[0]}
                  {x.lastName[0]}
                </div>
                <div className="min-w-0 flex-1">
                  <p className="font-medium">
                    {x.firstName} {x.lastName}
                  </p>
                  <p className="truncate text-sm text-zinc-500">
                    {x.phone ?? "Telefon yok"} · {x.email ?? "E-posta yok"}
                  </p>
                </div>
                <ChevronRight size={18} />
              </button>
            ))}
          </div>
        )}
      </Panel>
      <FormDialog
        open={!!selected}
        onOpenChange={(x) => !x && setSelected("")}
        title={
          detail.data
            ? `${detail.data.firstName} ${detail.data.lastName}`
            : "Eğitmen"
        }
        description={
          detail.data?.linkedUserName
            ? `Bağlı kullanıcı: ${detail.data.linkedUserName}`
            : "Identity kullanıcısı bağlı değil"
        }
      >
        {detail.isLoading ? (
          <Loading />
        ) : (
          <div>
            <div className="grid grid-cols-2 gap-3">
              <Stat
                label="Aktif ders"
                value={String(detail.data?.activeClassCount ?? 0)}
              />
              <Stat
                label="Haftalık seans"
                value={String(detail.data?.schedule.length ?? 0)}
              />
            </div>
            <div className="mt-5 space-y-2">
              {detail.data?.schedule.map((x) => (
                <Link
                  key={`${x.classId}-${x.startTime}`}
                  href={classDetailHref(x.classId)}
                  className="block rounded-lg border p-3 text-sm hover:bg-zinc-50"
                >
                  <b>{x.className}</b>
                  <span className="block text-zinc-500">
                    {scheduleDayLabel(x.dayOfWeek)} {displayTime(x.startTime)} ·{" "}
                    {x.roomName}
                  </span>
                </Link>
              ))}
            </div>
          </div>
        )}
      </FormDialog>
      <InstructorManagementDialog
        open={manage}
        onOpenChange={(x) => {
          setManage(x);
          if (!x) qc.invalidateQueries({ queryKey: classKeys.instructors });
        }}
      />
    </>
  );
}

function UsersPanel() {
  const qc = useQueryClient(),
    q = useQuery({
      queryKey: operationKeys.section("users"),
      queryFn: operationsApi.users,
    }),
    [editing, setEditing] = useState<UserRecord | null>(null);
  const mutation = useMutation({
    mutationFn: (x: {
      id: string;
      displayName: string;
      isActive: boolean;
      roles: string[];
    }) => operationsApi.updateUser(x.id, x),
    onSuccess: () => {
      toast.success("Kullanıcı güncellendi");
      setEditing(null);
      qc.invalidateQueries({ queryKey: ["operations"] });
    },
    onError: (e) => toast.error(e.message),
  });
  return (
    <>
      <Panel>
        {q.isLoading ? (
          <Loading />
        ) : q.isError ? (
          <ErrorState error={q.error} />
        ) : (
          <ResponsiveTable headers={["Ad", "E-posta", "Roller", "Durum", ""]}>
            {q.data?.map((x) => (
              <TableRow key={x.id}>
                <TableCell className="font-medium">{x.displayName}</TableCell>
                <TableCell>{x.email}</TableCell>
                <TableCell>{x.roles.join(" · ") || "Rol yok"}</TableCell>
                <TableCell>
                  <Tag>{x.isActive ? "Aktif" : "Pasif"}</Tag>
                </TableCell>
                <TableCell>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setEditing(x)}
                  >
                    Düzenle
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </ResponsiveTable>
        )}
      </Panel>
      <FormDialog
        open={!!editing}
        onOpenChange={(x) => !x && setEditing(null)}
        title="Kullanıcı erişimi"
        description="Mevcut Identity rollerini atayın."
      >
        {editing && (
          <form
            onSubmit={(e) => {
              e.preventDefault();
              const f = new FormData(e.currentTarget);
              mutation.mutate({
                id: editing.id,
                displayName: String(f.get("displayName")),
                isActive: f.get("isActive") === "on",
                roles: [
                  "Admin",
                  "Management",
                  "Reception",
                  "Instructor",
                ].filter((r) => f.get(r) === "on"),
              });
            }}
            className="space-y-4"
          >
            <Field label="Ad">
              <Input
                name="displayName"
                defaultValue={editing.displayName}
                required
              />
            </Field>
            <div className="grid grid-cols-2 gap-2">
              {["Admin", "Management", "Reception", "Instructor"].map((r) => (
                <label key={r} className="rounded-lg border p-3 text-sm">
                  <input
                    className="mr-2"
                    type="checkbox"
                    name={r}
                    defaultChecked={editing.roles.includes(r)}
                  />
                  {r}
                </label>
              ))}
            </div>
            <label className="flex gap-2 text-sm">
              <input
                type="checkbox"
                name="isActive"
                defaultChecked={editing.isActive}
              />
              Aktif kullanıcı
            </label>
            {mutation.isError && <MutationError error={mutation.error} />}
            <Button
              type="submit"
              className="w-full"
              disabled={mutation.isPending}
              aria-busy={mutation.isPending}
            >
              {mutation.isPending && <LoaderCircle className="animate-spin" />}
              {mutation.isPending ? "Kaydediliyor..." : "Kaydet"}
            </Button>
          </form>
        )}
      </FormDialog>
    </>
  );
}

function SettingsPanel() {
  const qc = useQueryClient(),
    [rooms, setRooms] = useState(false),
    [planOpen, setPlanOpen] = useState(false);
  const plans = useQuery({
      queryKey: operationKeys.section("all-plans"),
      queryFn: () => operationsApi.plans(false),
    }),
    mutation = useMutation({
      mutationFn: (x: unknown) => operationsApi.createPlan(x),
      onSuccess: () => {
        toast.success("Plan oluşturuldu");
        setPlanOpen(false);
        qc.invalidateQueries({ queryKey: ["operations"] });
      },
      onError: (e) => toast.error(e.message),
    });
  return (
    <>
      <div className="grid gap-5 md:grid-cols-2">
        <button
          type="button"
          onClick={() => setRooms(true)}
          className="rounded-xl border bg-white p-5 text-left shadow-sm hover:border-[#718360]"
        >
          <Settings className="text-[#61734f]" />
          <h2 className="mt-4 font-semibold">Salonlar</h2>
          <p className="mt-1 text-sm text-zinc-500">
            Kapasite ve aktiflik ayarları
          </p>
        </button>
        <button
          type="button"
          onClick={() => setPlanOpen(true)}
          className="rounded-xl border bg-white p-5 text-left shadow-sm hover:border-[#718360]"
        >
          <WalletCards className="text-[#61734f]" />
          <h2 className="mt-4 font-semibold">Yeni üyelik planı</h2>
          <p className="mt-1 text-sm text-zinc-500">
            Süre, ders adedi ve varsayılan fiyat
          </p>
        </button>
      </div>
      <Panel className="mt-5">
        {plans.isLoading ? (
          <Loading />
        ) : plans.isError ? (
          <ErrorState error={plans.error} />
        ) : (
          <ResponsiveTable
            headers={["Plan", "Tür", "Fiyat", "Ders", "Süre", "Durum"]}
          >
            {plans.data?.map((x) => (
              <TableRow key={x.id}>
                <TableCell className="font-medium">{x.name}</TableCell>
                <TableCell>{x.type}</TableCell>
                <TableCell>{money(x.defaultPrice)}</TableCell>
                <TableCell>{x.lessonCount ?? "—"}</TableCell>
                <TableCell>
                  {x.durationMonths ? `${x.durationMonths} ay` : "—"}
                </TableCell>
                <TableCell>
                  <Tag>{x.isActive ? "Aktif" : "Pasif"}</Tag>
                </TableCell>
              </TableRow>
            ))}
          </ResponsiveTable>
        )}
      </Panel>
      <RoomManagementDialog open={rooms} onOpenChange={setRooms} />
      <FormDialog
        open={planOpen}
        onOpenChange={setPlanOpen}
        title="Üyelik planı oluştur"
        description="Operasyonda kullanılacak plan ve fiyatı tanımlayın."
      >
        <form
          onSubmit={(e) => {
            e.preventDefault();
            const f = new FormData(e.currentTarget);
            mutation.mutate({
              name: f.get("name"),
              type: f.get("type"),
              defaultPrice: Number(f.get("price")),
              lessonCount: f.get("lessons") ? Number(f.get("lessons")) : null,
              durationMonths: f.get("months") ? Number(f.get("months")) : null,
              isActive: true,
            });
          }}
          className="space-y-4"
        >
          <Field label="Plan adı">
            <Input name="name" required />
          </Field>
          <Field label="Tür">
            <select name="type" className="control">
              <option value="Monthly">Aylık</option>
              <option value="LessonPackage">Ders paketi</option>
              <option value="PrivateLessonPackage">Özel ders paketi</option>
              <option value="Other">Diğer</option>
            </select>
          </Field>
          <div className="grid grid-cols-3 gap-3">
            <Field label="Fiyat">
              <Input name="price" type="number" min="0" step="0.01" required />
            </Field>
            <Field label="Ders">
              <Input name="lessons" type="number" min="0" />
            </Field>
            <Field label="Ay">
              <Input name="months" type="number" min="0" />
            </Field>
          </div>
          {mutation.isError && <MutationError error={mutation.error} />}
          <Button
            type="submit"
            className="w-full"
            disabled={mutation.isPending}
            aria-busy={mutation.isPending}
          >
            {mutation.isPending && <LoaderCircle className="animate-spin" />}
            {mutation.isPending ? "Kaydediliyor..." : "Planı oluştur"}
          </Button>
        </form>
      </FormDialog>
    </>
  );
}

function Panel({
  children,
  className = "",
}: {
  children: ReactNode;
  className?: string;
}) {
  return (
    <section
      className={`overflow-hidden rounded-xl border bg-white shadow-sm ${className}`}
    >
      {children}
    </section>
  );
}
function Toolbar({
  search,
  setSearch,
  placeholder,
  children,
}: {
  search: string;
  setSearch: (x: string) => void;
  placeholder: string;
  children?: ReactNode;
}) {
  return (
    <div className="mb-4 flex flex-col gap-3 sm:flex-row">
      <div className="relative flex-1">
        <Search
          className="absolute left-3 top-1/2 -translate-y-1/2 text-zinc-400"
          size={17}
        />
        <Input
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder={placeholder}
          className="pl-9"
        />
      </div>
      {children}
    </div>
  );
}
function Filters({ children }: { children: ReactNode }) {
  return (
    <div className="grid grid-cols-2 gap-3 border-b p-4 lg:grid-cols-4">
      {children}
    </div>
  );
}
function Select({
  label,
  value,
  onChange,
  options,
}: {
  label: string;
  value: string;
  onChange: (x: string) => void;
  options: { value: string; label: string }[];
}) {
  return (
    <select
      aria-label={label}
      value={value}
      onChange={(e) => onChange(e.target.value)}
      className="control"
    >
      <option value="">Tüm {label.toLocaleLowerCase("tr-TR")}</option>
      {options.map((x) => (
        <option key={x.value} value={x.value}>
          {x.label}
        </option>
      ))}
    </select>
  );
}
function ResponsiveTable({
  headers,
  children,
}: {
  headers: string[];
  children: ReactNode;
}) {
  return (
    <div className="overflow-x-auto">
      <Table>
        <TableHeader>
          <TableRow className="bg-zinc-50">
            {headers.map((x, i) => (
              <TableHead key={i}>{x}</TableHead>
            ))}
          </TableRow>
        </TableHeader>
        <TableBody>{children}</TableBody>
      </Table>
    </div>
  );
}
function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl border bg-white p-4 shadow-sm">
      <p className="text-xs text-zinc-500">{label}</p>
      <p className="mt-2 text-xl font-semibold">{value}</p>
    </div>
  );
}
function Tag({ children }: { children: ReactNode }) {
  return (
    <span className="inline-flex rounded-full bg-zinc-100 px-2.5 py-1 text-xs font-medium text-zinc-700">
      {children}
    </span>
  );
}
function Loading() {
  return (
    <div className="space-y-3 p-5">
      {Array.from({ length: 5 }).map((_, i) => (
        <div key={i} className="h-14 animate-pulse rounded-lg bg-zinc-100" />
      ))}
    </div>
  );
}
function Empty({
  icon,
  title,
  detail,
}: {
  icon: ReactNode;
  title: string;
  detail: string;
}) {
  return (
    <div className="grid min-h-72 place-items-center p-8 text-center">
      <div>
        <div className="mx-auto mb-4 grid size-12 place-items-center rounded-full bg-zinc-100 text-zinc-400">
          {icon}
        </div>
        <h2 className="font-medium">{title}</h2>
        <p className="mt-2 text-sm text-zinc-500">{detail}</p>
      </div>
    </div>
  );
}
function ErrorState({ error }: { error: Error }) {
  return (
    <Empty
      icon={<CircleAlert />}
      title="Veriler yüklenemedi"
      detail={error.message}
    />
  );
}
function MutationError({ error }: { error: unknown }) {
  return (
    <p
      role="alert"
      className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700"
    >
      {formErrorMessage(error, "İşlem sırasında beklenmeyen bir hata oluştu.")}
    </p>
  );
}
function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <Label className="mb-2 block">{label}</Label>
      {children}
    </div>
  );
}
function FormDialog({
  open,
  onOpenChange,
  title,
  description,
  children,
}: {
  open: boolean;
  onOpenChange: (x: boolean) => void;
  title: string;
  description: string;
  children: ReactNode;
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>
        {children}
      </DialogContent>
    </Dialog>
  );
}
