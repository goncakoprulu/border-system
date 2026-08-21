import { Badge } from "@/components/ui/badge";
import { ClassStatus, classStatusLabels } from "@/lib/classes";

const styles: Record<ClassStatus, string> = { Planned: "bg-blue-50 text-blue-700", Active: "bg-emerald-50 text-emerald-700", Paused: "bg-amber-50 text-amber-700", Completed: "bg-zinc-100 text-zinc-600", Cancelled: "bg-red-50 text-red-700" };
export function ClassStatusBadge({ status }: { status: ClassStatus }) { return <Badge variant="secondary" className={styles[status]}>{classStatusLabels[status]}</Badge>; }
