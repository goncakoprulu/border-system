import { Suspense } from "react";
import { StudentDetailClient } from "./student-detail-client";

export default function StudentDetailPage() {
  return <Suspense fallback={<div className="mx-auto max-w-6xl space-y-5"><div className="h-28 animate-pulse rounded-xl bg-zinc-200" /><div className="h-80 animate-pulse rounded-xl bg-zinc-100" /></div>}><StudentDetailClient /></Suspense>;
}
