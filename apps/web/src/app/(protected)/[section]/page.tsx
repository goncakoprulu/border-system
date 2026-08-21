import { Construction } from "lucide-react";
import { notFound } from "next/navigation";

const labels: Record<string, string> = {
  students: "Öğrenciler", classes: "Sınıflar", schedule: "Program", attendance: "Yoklama",
  memberships: "Üyelikler", payments: "Ödemeler", balances: "Borç Bakiyeleri", reports: "Raporlar",
  instructors: "Eğitmenler", users: "Kullanıcılar", settings: "Ayarlar", "my-classes": "Sınıflarım",
};

const placeholderSections = [
  "schedule", "attendance", "memberships", "payments", "balances",
  "reports", "instructors", "users", "settings",
] as const;

export const dynamicParams = false;

export function generateStaticParams() {
  return placeholderSections.map((section) => ({ section }));
}

export default async function PlaceholderPage({ params }: { params: Promise<{ section: string }> }) {
  const { section } = await params;
  const title = labels[section];
  if (!title) notFound();
  return <div className="mx-auto max-w-6xl"><h1 className="text-2xl font-semibold tracking-tight sm:text-3xl">{title}</h1><div className="mt-8 flex min-h-64 items-center justify-center rounded-2xl border border-dashed bg-white/60 p-8 text-center"><div><Construction className="mx-auto mb-4 text-zinc-400" /><p className="font-medium">Bu modül sonraki fazlar için hazırlandı.</p><p className="mt-2 text-sm text-zinc-500">Bu aşamada sahte veri veya geçici iş akışı eklenmedi.</p></div></div></div>;
}
