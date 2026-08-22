import { apiQuery } from "@/lib/api";

export type ReportMetric = { value: number; trendPercent: number | null };
export type ReportPoint = { label: string; value: number };
export type ReportSummary = {
  activeStudents: ReportMetric;
  newStudents: ReportMetric;
  activeMemberships: ReportMetric;
  totalRevenue: ReportMetric;
  outstandingBalance: ReportMetric;
  attendanceRate: ReportMetric;
};
export type ReportFinance = {
  revenue: {
    total: number;
    paymentCount: number;
    averagePayment: number;
    methods: { method: string; count: number; amount: number }[];
    trend: ReportPoint[];
    peakLabel: string | null;
    peakAmount: number | null;
  };
  balances: {
    totalInvoiced: number;
    totalPaid: number;
    outstandingBalance: number;
    overdueBalance: number;
    overdueInvoiceCount: number;
    statuses: { status: string; count: number; amount: number }[];
    topDebtors: {
      studentId: string;
      studentName: string;
      invoiced: number;
      paid: number;
      outstanding: number;
    }[];
  };
};
export type ReportEngagement = {
  students: {
    total: number;
    active: number;
    trial: number;
    frozen: number;
    passive: number;
    left: number;
    newStudents: number;
    statuses: ReportPoint[];
    newStudentTrend: ReportPoint[];
  };
  attendance: {
    total: number;
    present: number;
    absent: number;
    excused: number;
    late: number;
    makeUp: number;
    rate: number;
    missingSessions: number;
    trend: ReportPoint[];
    classes: {
      classId: string;
      className: string;
      total: number;
      rate: number;
    }[];
  };
};
export type ReportCapacity = {
  classes: {
    classId: string;
    className: string;
    instructorId: string;
    instructorName: string;
    roomName: string;
    capacity: number;
    activeStudents: number;
    occupancyRate: number;
  }[];
  instructors: {
    instructorId: string;
    instructorName: string;
    activeClasses: number;
    totalStudents: number;
    sessions: number;
    averageOccupancy: number;
    attendanceRate: number;
  }[];
  memberships: {
    active: number;
    frozen: number;
    expired: number;
    cancelled: number;
    plans: {
      planId: string;
      planName: string;
      activeStudents: number;
      totalInvoiced: number;
      averagePrice: number;
      discountedMemberships: number;
    }[];
    expiring: {
      membershipId: string;
      studentId: string;
      studentName: string;
      planName: string;
      endDate: string;
      daysRemaining: number;
    }[];
  };
};

export const reportKeys = {
  all: ["management-reports"] as const,
  section: (name: string, params: string) =>
    ["management-reports", name, params] as const,
};
export const reportsApi = {
  summary: (params: string) =>
    apiQuery<ReportSummary>(`/api/reports/summary?${params}`),
  finance: (params: string) =>
    apiQuery<ReportFinance>(`/api/reports/finance?${params}`),
  engagement: (params: string) =>
    apiQuery<ReportEngagement>(`/api/reports/engagement?${params}`),
  capacity: (params: string) =>
    apiQuery<ReportCapacity>(`/api/reports/capacity?${params}`),
};
