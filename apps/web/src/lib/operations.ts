import { apiMutation, apiQuery } from "@/lib/api";

export type ScheduleItem = { classId:string; className:string; instructorName:string; instructorId:string; roomName:string; roomId:string; dayOfWeek:number; startTime:string; endTime:string; level:string|null };
export type Session = { id:string; classId:string; className:string; instructorName:string; roomName:string; scheduledStart:string; scheduledEnd:string; studentCount:number; isCompleted:boolean };
export type AttendanceStatus = "Present"|"Absent"|"Excused"|"Late"|"MakeUp";
export type AttendanceStudent = { studentId:string; studentName:string; status:AttendanceStatus|null; notes:string|null };
export type AttendanceDetail = { session:Session; students:AttendanceStudent[] };
export type Membership = { id:string; studentId:string; studentName:string; planId:string; planName:string; planType:string; startDate:string; endDate:string|null; status:string; price:number; remainingLessons:number|null };
export type Plan = { id:string; name:string; type:string; defaultPrice:number; lessonCount:number|null; durationMonths:number|null; isActive:boolean };
export type Invoice = { id:string; description:string; amount:number; paid:number; remaining:number; dueDate:string };
export type Payment = { id:string; studentId:string; studentName:string; amount:number; paymentDate:string; paymentMethod:string; invoiceId:string|null; invoiceDescription:string|null; notes:string|null };
export type Balance = { studentId:string; studentName:string; totalDebt:number; paid:number; remaining:number; lastPaymentDate:string|null };
export type Balances = { summary:{openBalance:number; debtorCount:number; collectedThisMonth:number; overdueTotal:number}; items:Balance[] };
export type Reports = { activeStudents:number; activeClasses:number; collectedThisMonth:number; openBalance:number; averageOccupancy:number; attendanceRate:number; monthlyCollections:{label:string;value:number}[]; studentStatuses:{label:string;value:number}[]; classOccupancies:{label:string;value:number}[] };
export type InstructorDetail = { id:string; firstName:string; lastName:string; phone:string|null; email:string|null; userId:string|null; linkedUserName:string|null; isArchived:boolean; activeClassCount:number; schedule:ScheduleItem[] };
export type UserRecord = { id:string; displayName:string; email:string; roles:string[]; isActive:boolean };

export const operationKeys = { section:(name:string, suffix="") => ["operations",name,suffix] as const };
export const operationsApi = {
  schedule:(params="") => apiQuery<ScheduleItem[]>(`/api/schedule${params ? `?${params}` : ""}`),
  sessions:(date:string) => apiQuery<Session[]>(`/api/attendance/sessions?date=${date}`),
  attendance:(id:string) => apiQuery<AttendanceDetail>(`/api/attendance/sessions/${id}`),
  saveAttendance:(id:string, entries:{studentId:string;status:AttendanceStatus;notes:string|null}[]) => apiMutation<AttendanceDetail>(`/api/attendance/sessions/${id}`,"PUT",{entries}),
  memberships:(params="") => apiQuery<Membership[]>(`/api/memberships${params ? `?${params}` : ""}`),
  createMembership:(input:unknown) => apiMutation<Membership>("/api/memberships","POST",input),
  plans:(activeOnly=true) => apiQuery<Plan[]>(`/api/membership-plans?activeOnly=${activeOnly}`),
  createPlan:(input:unknown) => apiMutation<Plan>("/api/membership-plans","POST",input),
  updatePlan:(id:string,input:unknown) => apiMutation<Plan>(`/api/membership-plans/${id}`,"PUT",input),
  payments:(params="") => apiQuery<Payment[]>(`/api/payments${params ? `?${params}` : ""}`),
  invoices:(studentId:string) => apiQuery<Invoice[]>(`/api/students/${studentId}/open-invoices`),
  createPayment:(input:unknown) => apiMutation<Payment>("/api/payments","POST",input),
  balances:(search="") => apiQuery<Balances>(`/api/balances${search ? `?search=${encodeURIComponent(search)}` : ""}`),
  reports:() => apiQuery<Reports>("/api/reports"),
  instructor:(id:string) => apiQuery<InstructorDetail>(`/api/instructors/${id}`),
  users:() => apiQuery<UserRecord[]>("/api/users"),
  updateUser:(id:string,input:unknown) => apiMutation<UserRecord>(`/api/users/${id}`,"PUT",input),
};
