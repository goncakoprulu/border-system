"use client";

import { CheckCircle2 } from "lucide-react";
import { useCurrentUser } from "@/hooks/use-current-user";

export default function DashboardPage() {
  const { data: user } = useCurrentUser();
  const firstName = user?.displayName.split(" ")[0] ?? "";
  return <div className="mx-auto max-w-6xl">
    <div className="mb-8"><p className="text-sm font-medium text-[#61734f]">Genel Bakış</p><h1 className="mt-1 text-2xl font-semibold tracking-tight sm:text-3xl">Hoş geldiniz, {firstName}</h1></div>
    <section className="flex min-h-64 items-center rounded-2xl border bg-white p-7 shadow-sm sm:p-10">
      <div className="max-w-xl"><div className="mb-5 grid size-12 place-items-center rounded-full bg-[#edf2e8] text-[#526743]"><CheckCircle2 /></div><h2 className="text-xl font-semibold">BORDER yönetim sistemi hazır.</h2><p className="mt-3 leading-7 text-zinc-500">Temel altyapı güvenli biçimde çalışıyor. Gerçek verilere dayalı yönetim modülleri sonraki fazlarda bu alana eklenecek.</p></div>
    </section>
  </div>;
}
