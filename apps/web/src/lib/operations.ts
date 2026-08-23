import { apiMutation, apiQuery } from "@/lib/api";

export type ScheduleItem = { classId:string; className:string; instructorName:string; instructorId:string; roomName:string; roomId:string; dayOfWeek:number; startTime:string; endTime:string; level:string|null };
export type Session = { id:string; classId:string; className:string; instructorId:string; instructorName:string; roomId:string; roomName:string; scheduledStart:string; scheduledEnd:string; studentCount:number; recordedCount:number; isCompleted:boolean };
export type AttendanceStatus = "Present"|"Absent"|"Excused"|"Late"|"MakeUp";
export type AttendanceStudent = { studentId:string; studentName:string; status:AttendanceStatus|null; notes:string|null };
export type AttendanceDetail = { session:Session; students:AttendanceStudent[] };
export type StudentAttendanceHistory = { total:number; present:number; absent:number; excused:number; late:number; makeUp:number; attendanceRate:number; items:{attendanceId:string;sessionId:string;classId:string;className:string;scheduledStart:string;status:AttendanceStatus;notes:string|null}[] };
export type Membership = { id:string; studentId:string; studentName:string; planId:string; planName:string; planType:string; startDate:string; endDate:string|null; status:string; price:number; remainingLessons:number|null };
export type Plan = { id:string; name:string; type:string; defaultPrice:number; lessonCount:number|null; durationMonths:number|null; isActive:boolean };
export type Invoice = { id:string; description:string; amount:number; paid:number; remaining:number; dueDate:string; status:string };
export type Payment = { id:string; studentId:string; studentName:string; amount:number; paymentDate:string; paymentMethod:string; invoiceId:string|null; invoiceDescription:string|null; notes:string|null };
export type DebtStatus = "None"|"Open"|"Overdue";
export type Balance = { studentId:string; studentName:string; totalDebt:number; paid:number; remaining:number; lastPaymentDate:string|null; overdueBalance:number; openInvoiceCount:number; overdueInvoiceCount:number; status:DebtStatus };
export type Balances = { summary:{openBalance:number; debtorCount:number; collectedThisMonth:number; overdueTotal:number}; items:Balance[] };
export type BalanceFilters = { search?:string; overdueOnly?:boolean; openOnly?:boolean; includeSettled?:boolean };
export type StudentFinanceOverview = { totalInvoiced:number; totalPaid:number; openBalance:number; overdueBalance:number; memberships:{id:string;planId:string;planName:string;startDate:string;endDate:string|null;status:string;price:number;discountAmount:number|null;discountReason:string|null}[]; invoices:{id:string;description:string;amount:number;paid:number;remaining:number;dueDate:string;status:string}[]; payments:{id:string;invoiceId:string|null;invoiceDescription:string|null;amount:number;paymentDate:string;paymentMethod:string;notes:string|null}[] };
export type Reports = { activeStudents:number; activeClasses:number; collectedThisMonth:number; openBalance:number; averageOccupancy:number; attendanceRate:number; monthlyCollections:{label:string;value:number}[]; studentStatuses:{label:string;value:number}[]; classOccupancies:{label:string;value:number}[] };
export type DashboardLesson = { sessionId:string; classId:string; className:string; instructorName:string; roomName:string; scheduledStart:string; scheduledEnd:string; studentCount:number; capacity:number; isAttendanceCompleted:boolean };
export type DashboardOperations = { activeStudentCount:number; todayLessonCount:number; todayLessons:DashboardLesson[] };
export type DashboardAnalytics = { canViewFinance:boolean; monthlyRevenue:number; outstandingBalance:number; attendanceRate:number; newStudents:number; totalPayments:number; activeMemberships:number; alerts:{type:string;count:number;label:string;href:string}[]; thirtyDayRevenue:{label:string;value:number}[] };
export type InstructorDetail = { id:string; firstName:string; lastName:string; phone:string|null; email:string|null; userId:string|null; linkedUserName:string|null; isArchived:boolean; activeClassCount:number; schedule:ScheduleItem[] };
export type UserRecord = { id:string; displayName:string; email:string; roles:string[]; isActive:boolean };

export const operationKeys = { section:(name:string, suffix="") => ["operations",name,suffix] as const };
export const operationsApi = {
  dashboardOperations:() => apiQuery<DashboardOperations>("/api/dashboard/operations"),
  dashboardAnalytics:() => apiQuery<DashboardAnalytics>("/api/dashboard/analytics"),
  schedule:(params="") => apiQuery<ScheduleItem[]>(`/api/schedule${params ? `?${params}` : ""}`),
  sessions:(params:string) => apiQuery<Session[]>(`/api/attendance/sessions?${params}`),
  attendance:(id:string) => apiQuery<AttendanceDetail>(`/api/attendance/sessions/${id}`),
  saveAttendance:(id:string, entries:{studentId:string;status:AttendanceStatus;notes:string|null}[]) => apiMutation<AttendanceDetail>(`/api/attendance/sessions/${id}`,"PUT",{entries}),
  studentAttendance:(studentId:string) => apiQuery<StudentAttendanceHistory>(`/api/students/${studentId}/attendance-history`),
  memberships:(params="") => apiQuery<Membership[]>(`/api/memberships${params ? `?${params}` : ""}`),
  createMembership:(input:unknown) => apiMutation<Membership>("/api/memberships","POST",input),
  plans:(activeOnly=true) => apiQuery<Plan[]>(`/api/membership-plans?activeOnly=${activeOnly}`),
  createPlan:(input:unknown) => apiMutation<Plan>("/api/membership-plans","POST",input),
  updatePlan:(id:string,input:unknown) => apiMutation<Plan>(`/api/membership-plans/${id}`,"PUT",input),
  payments:(params="") => apiQuery<Payment[]>(`/api/payments${params ? `?${params}` : ""}`),
  invoices:(studentId:string) => apiQuery<Invoice[]>(`/api/students/${studentId}/open-invoices`),
  createPayment:(input:unknown) => apiMutation<Payment>("/api/payments","POST",input),
  balances:(filters:BalanceFilters={}) => {
    const params = new URLSearchParams();
    if (filters.search?.trim()) params.set("search",filters.search.trim());
    if (filters.overdueOnly) params.set("overdueOnly","true");
    if (filters.openOnly) params.set("openOnly","true");
    if (filters.includeSettled) params.set("includeSettled","true");
    const query=params.toString();
    return apiQuery<Balances>(`/api/balances${query?`?${query}`:""}`);
  },
  studentFinance:(studentId:string) => apiQuery<StudentFinanceOverview>(`/api/students/${studentId}/finance-overview`),
  reports:() => apiQuery<Reports>("/api/reports"),
  instructor:(id:string) => apiQuery<InstructorDetail>(`/api/instructors/${id}`),
  users:() => apiQuery<UserRecord[]>("/api/users"),
  updateUser:(id:string,input:unknown) => apiMutation<UserRecord>(`/api/users/${id}`,"PUT",input),
};
