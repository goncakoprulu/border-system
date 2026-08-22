import { apiMutation, apiQuery } from "@/lib/api";
import { scheduleDayLabels, scheduleDayTimeText } from "@/lib/schedule-days";

export const classStatuses = ["Planned", "Active", "Paused", "Completed", "Cancelled"] as const;
export type ClassStatus = (typeof classStatuses)[number];
export const classStatusLabels: Record<ClassStatus, string> = { Planned: "Planlandı", Active: "Aktif", Paused: "Duraklatıldı", Completed: "Tamamlandı", Cancelled: "İptal" };
export const enrollmentStatusLabels = { Active: "Aktif", Frozen: "Donduruldu", Completed: "Tamamlandı", Cancelled: "İptal" } as const;
export const dayLabels = scheduleDayLabels;

export type ClassSchedule = { id: string; dayOfWeek: number; startTime: string; endTime: string };
export type ClassListItem = { id: string; name: string; instructorName: string; roomName: string; capacity: number; activeStudentCount: number; status: ClassStatus; startDate: string; isArchived: boolean; schedules: ClassSchedule[] };
export type ClassEnrollment = { id: string; studentId: string; studentName: string; phone: string | null; studentStatus: string; startDate: string; endDate: string | null; status: keyof typeof enrollmentStatusLabels };
export type ClassDetail = ClassListItem & { description: string | null; instructorId: string; studioRoomId: string; level: string | null; ageGroup: string | null; endDate: string | null; enrollments: ClassEnrollment[] };
export type ClassInput = { name: string; description: string | null; instructorId: string; studioRoomId: string; capacity: number; level: string | null; ageGroup: string | null; status: ClassStatus; startDate: string; endDate: string | null; schedules: { dayOfWeek: number; startTime: string; endTime: string }[] };
export type InstructorOption = { id: string; fullName: string; userId: string | null };
export type InstructorRecord = { id: string; firstName: string; lastName: string; phone: string | null; email: string | null; userId: string | null; isArchived: boolean };
export type InstructorLoginOption = { userId: string; displayName: string; email: string; linkedInstructorId: string | null };
export type InstructorInput = { firstName: string; lastName: string; phone: string | null; email: string | null; userId: string | null };
export type StudioRoom = { id: string; name: string; description: string | null; capacity: number | null; isActive: boolean; isArchived: boolean };
export type RoomInput = { name: string; description: string | null; capacity: number | null; isActive: boolean };
export type PagedClasses = { items: ClassListItem[]; page: number; pageSize: number; totalCount: number; totalPages: number };

export const classKeys = { all: ["classes"] as const, list: (params: string) => ["classes", "list", params] as const, detail: (id: string) => ["classes", "detail", id] as const, rooms: ["classes", "rooms"] as const, instructors: ["classes", "instructors"] as const };
export const classesApi = {
  list: (params: URLSearchParams) => apiQuery<PagedClasses>(`/api/classes?${params}`),
  detail: (id: string, includeArchived = false) => apiQuery<ClassDetail>(`/api/classes/${id}${includeArchived ? "?includeArchived=true" : ""}`),
  create: (input: ClassInput) => apiMutation<ClassDetail>("/api/classes", "POST", input),
  update: (id: string, input: ClassInput) => apiMutation<ClassDetail>(`/api/classes/${id}`, "PUT", input),
  changeStatus: (id: string, status: ClassStatus) => apiMutation<ClassDetail>(`/api/classes/${id}/status`, "PATCH", { status }),
  archive: (id: string) => apiMutation<void>(`/api/classes/${id}`, "DELETE"),
  enroll: (id: string, studentId: string, startDate: string) => apiMutation<ClassEnrollment>(`/api/classes/${id}/enrollments`, "POST", { studentId, startDate }),
  endEnrollment: (id: string, enrollmentId: string, endDate: string | null) => apiMutation<ClassEnrollment>(`/api/classes/${id}/enrollments/${enrollmentId}/end`, "PATCH", { endDate }),
  instructors: () => apiQuery<InstructorOption[]>("/api/instructors/options"),
  instructorRecords: () => apiQuery<InstructorRecord[]>("/api/instructors"),
  instructorLogins: () => apiQuery<InstructorLoginOption[]>("/api/instructors/login-options"),
  createInstructor: (input: InstructorInput) => apiMutation<InstructorRecord>("/api/instructors", "POST", input),
  updateInstructor: (id: string, input: InstructorInput) => apiMutation<InstructorRecord>(`/api/instructors/${id}`, "PUT", input),
  archiveInstructor: (id: string) => apiMutation<void>(`/api/instructors/${id}`, "DELETE"),
  rooms: () => apiQuery<StudioRoom[]>("/api/rooms"),
  createRoom: (input: RoomInput) => apiMutation<StudioRoom>("/api/rooms", "POST", input),
  updateRoom: (id: string, input: RoomInput) => apiMutation<StudioRoom>(`/api/rooms/${id}`, "PUT", input),
};

export const displayTime = (value: string) => value.slice(0, 5);
export const scheduleText = (schedules: ClassSchedule[]) => schedules.length ? schedules.map((item) => scheduleDayTimeText(item.dayOfWeek, item.startTime, item.endTime)).join(", ") : "Program eklenmedi";
