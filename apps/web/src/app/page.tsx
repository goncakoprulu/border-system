"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect } from "react";

export default function Home() {
  const router = useRouter();
  useEffect(() => { router.replace("/dashboard/"); }, [router]);
  return <main className="grid min-h-screen place-items-center bg-[#f7f7f5] p-6 text-center"><div><p className="text-sm text-zinc-500">BORDER Yönetim Sistemi açılıyor.</p><Link href="/dashboard/" className="mt-3 inline-flex text-sm font-medium text-[#526743] underline underline-offset-4">Panele devam et</Link></div></main>;
}
