import { Suspense } from "react";
import { ClassDetailClient } from "./class-detail-client";

export default function ClassDetailPage() {
  return <Suspense fallback={<div className="mx-auto max-w-6xl"><div className="h-80 animate-pulse rounded-xl bg-zinc-100" /></div>}><ClassDetailClient /></Suspense>;
}
