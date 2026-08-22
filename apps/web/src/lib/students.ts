import { apiMutation, apiQuery } from "@/lib/api";

export const studentStatuses = ["Lead", "Trial", "Active", "Frozen", "Passive", "Left"] as const;
export type StudentStatus = (typeof studentStatuses)[number];

export const studentStatusLabels: Record<StudentStatus, string> = {
  Lead: "Aday", Trial: "Deneme", Active: "Aktif", Frozen: "Dondurulmuş", Passive: "Pasif", Left: "Ayrıldı",
};

export type StudentListItem = {
  id: string; firstName: string; lastName: string; phone: string | null; email: string | null;
  status: StudentStatus; registrationDate: string; isArchived: boolean;
};
export type Guardian = {
  id: string; studentId: string; firstName: string; lastName: string; relationship: string; phone: string | null; email: string | null;
};
export type StudentDetail = StudentListItem & {
  birthDate: string | null; gender: string | null; notes: string | null; createdAt: string; updatedAt: string; guardians: Guardian[];
  classEnrollments: StudentClassEnrollment[];
};
export type StudentClassEnrollment = { enrollmentId: string; classId: string; className: string; instructorName: string; roomName: string; startDate: string; endDate: string | null; status: "Active" | "Frozen" | "Completed" | "Cancelled"; schedules: { dayOfWeek: number; startTime: string; endTime: string }[] };
export type StudentInput = {
  firstName: string; lastName: string; phone: string | null; email: string | null; birthDate: string | null;
  gender: string | null; notes: string | null; status: StudentStatus; registrationDate: string;
};
export type GuardianInput = { firstName: string; lastName: string; relationship: string; phone: string | null; email: string | null };
export type PagedStudents = { items: StudentListItem[]; page: number; pageSize: number; totalCount: number; totalPages: number };
export type CreateStudentResult = { student: StudentDetail; duplicateWarnings: { id: string; fullName: string; matchedOn: string }[] };

export const studentKeys = {
  all: ["students"] as const,
  list: (params: string) => ["students", "list", params] as const,
  detail: (id: string) => ["students", "detail", id] as const,
};

export const studentsApi = {
  list: (params: URLSearchParams) => apiQuery<PagedStudents>(`/api/students?${params}`),
  detail: (id: string, includeArchived = false) => apiQuery<StudentDetail>(`/api/students/${id}${includeArchived ? "?includeArchived=true" : ""}`),
  create: (input: StudentInput) => apiMutation<CreateStudentResult>("/api/students", "POST", input),
  update: (id: string, input: StudentInput) => apiMutation<StudentDetail>(`/api/students/${id}`, "PUT", input),
  changeStatus: (id: string, status: StudentStatus) => apiMutation<StudentDetail>(`/api/students/${id}/status`, "PATCH", { status }),
  archive: (id: string) => apiMutation<void>(`/api/students/${id}`, "DELETE"),
  addGuardian: (studentId: string, input: GuardianInput) => apiMutation<Guardian>(`/api/students/${studentId}/guardians`, "POST", input),
  updateGuardian: (studentId: string, guardianId: string, input: GuardianInput) => apiMutation<Guardian>(`/api/students/${studentId}/guardians/${guardianId}`, "PUT", input),
  deleteGuardian: (studentId: string, guardianId: string) => apiMutation<void>(`/api/students/${studentId}/guardians/${guardianId}`, "DELETE"),
};

export function formatDate(value: string | null) {
  if (!value) return "—";
  return new Intl.DateTimeFormat("tr-TR", { timeZone: "Europe/Istanbul", day: "2-digit", month: "long", year: "numeric" }).format(new Date(`${value}T12:00:00Z`));
}

export function calculateAge(birthDate: string | null) {
  if (!birthDate) return null;
  const birth = new Date(`${birthDate}T12:00:00Z`);
  const now = new Date();
  const parts = new Intl.DateTimeFormat("en-CA", { timeZone: "Europe/Istanbul", year: "numeric", month: "2-digit", day: "2-digit" }).formatToParts(now);
  const get = (type: Intl.DateTimeFormatPartTypes) => Number(parts.find((part) => part.type === type)?.value);
  let age = get("year") - birth.getUTCFullYear();
  if (get("month") < birth.getUTCMonth() + 1 || (get("month") === birth.getUTCMonth() + 1 && get("day") < birth.getUTCDate())) age--;
  return age;
}
