using Border.Domain.Entities;

namespace Border.Application.Operations;

public sealed record ReportFilter(DateOnly From, DateOnly To, Guid? InstructorId, Guid? ClassId, Guid? RoomId);
public sealed record ReportMetric(decimal Value, decimal? TrendPercent);
public sealed record ReportingSummaryResponse(ReportMetric ActiveStudents, ReportMetric NewStudents, ReportMetric ActiveMemberships, ReportMetric TotalRevenue, ReportMetric OutstandingBalance, ReportMetric AttendanceRate);
public sealed record ReportSeriesPoint(string Label, decimal Value);
public sealed record PaymentMethodReport(PaymentMethod Method, int Count, decimal Amount);
public sealed record RevenueReport(decimal Total, int PaymentCount, decimal AveragePayment, IReadOnlyCollection<PaymentMethodReport> Methods, IReadOnlyCollection<ReportSeriesPoint> Trend, string? PeakLabel, decimal? PeakAmount);
public sealed record InvoiceStatusReport(InvoiceStatus Status, int Count, decimal Amount);
public sealed record StudentBalanceReport(Guid StudentId, string StudentName, decimal Invoiced, decimal Paid, decimal Outstanding);
public sealed record BalanceReport(decimal TotalInvoiced, decimal TotalPaid, decimal OutstandingBalance, decimal OverdueBalance, int OverdueInvoiceCount, IReadOnlyCollection<InvoiceStatusReport> Statuses, IReadOnlyCollection<StudentBalanceReport> TopDebtors);
public sealed record ReportingFinanceResponse(RevenueReport Revenue, BalanceReport Balances);
public sealed record StudentReport(int Total, int Active, int Trial, int Frozen, int Passive, int Left, int NewStudents, IReadOnlyCollection<ReportSeriesPoint> Statuses, IReadOnlyCollection<ReportSeriesPoint> NewStudentTrend);
public sealed record AttendanceClassReport(Guid ClassId, string ClassName, int Total, decimal Rate);
public sealed record AttendanceReport(int Total, int Present, int Absent, int Excused, int Late, int MakeUp, decimal Rate, int MissingSessions, IReadOnlyCollection<ReportSeriesPoint> Trend, IReadOnlyCollection<AttendanceClassReport> Classes);
public sealed record ReportingEngagementResponse(StudentReport Students, AttendanceReport Attendance);
public sealed record ClassOccupancyReport(Guid ClassId, string ClassName, Guid InstructorId, string InstructorName, string RoomName, int Capacity, int ActiveStudents, decimal OccupancyRate);
public sealed record InstructorReport(Guid InstructorId, string InstructorName, int ActiveClasses, int TotalStudents, int Sessions, decimal AverageOccupancy, decimal AttendanceRate);
public sealed record MembershipPlanReport(Guid PlanId, string PlanName, int ActiveStudents, decimal TotalInvoiced, decimal AveragePrice, int DiscountedMemberships);
public sealed record ExpiringMembershipReport(Guid MembershipId, Guid StudentId, string StudentName, string PlanName, DateOnly EndDate, int DaysRemaining);
public sealed record MembershipReport(int Active, int Frozen, int Expired, int Cancelled, IReadOnlyCollection<MembershipPlanReport> Plans, IReadOnlyCollection<ExpiringMembershipReport> Expiring);
public sealed record ReportingCapacityResponse(IReadOnlyCollection<ClassOccupancyReport> Classes, IReadOnlyCollection<InstructorReport> Instructors, MembershipReport Memberships);

public interface IReportingService
{
    Task<ReportingSummaryResponse> GetSummaryAsync(ReportFilter filter, CancellationToken ct);
    Task<ReportingFinanceResponse> GetFinanceAsync(ReportFilter filter, CancellationToken ct);
    Task<ReportingEngagementResponse> GetEngagementAsync(ReportFilter filter, CancellationToken ct);
    Task<ReportingCapacityResponse> GetCapacityAsync(ReportFilter filter, CancellationToken ct);
}
