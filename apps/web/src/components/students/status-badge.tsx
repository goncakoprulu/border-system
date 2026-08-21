import { Badge } from "@/components/ui/badge";
import { StudentStatus, studentStatusLabels } from "@/lib/students";

const styles: Record<StudentStatus, string> = {
  Lead: "border-sky-200 bg-sky-50 text-sky-700",
  Trial: "border-violet-200 bg-violet-50 text-violet-700",
  Active: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Frozen: "border-blue-200 bg-blue-50 text-blue-700",
  Passive: "border-zinc-200 bg-zinc-100 text-zinc-600",
  Left: "border-amber-200 bg-amber-50 text-amber-700",
};

export function StatusBadge({ status }: { status: StudentStatus }) {
  return <Badge variant="outline" className={`font-medium ${styles[status]}`}>{studentStatusLabels[status]}</Badge>;
}
