using Border.Domain.Entities;

namespace Border.Application.Operations;

public sealed record ScheduleItemResponse(Guid ClassId, string ClassName, string InstructorName, Guid InstructorId, string RoomName, Guid RoomId, DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime, string? Level);
public sealed record SessionListItemResponse(Guid Id, Guid ClassId, string ClassName, string InstructorName, string RoomName, DateTime ScheduledStart, DateTime ScheduledEnd, int StudentCount, bool IsCompleted);
public sealed record AttendanceStudentResponse(Guid StudentId, string StudentName, AttendanceStatus? Status, string? Notes);
public sealed record AttendanceDetailResponse(SessionListItemResponse Session, IReadOnlyCollection<AttendanceStudentResponse> Students);
public sealed record AttendanceEntryRequest(Guid StudentId, AttendanceStatus Status, string? Notes);
public sealed record SaveAttendanceRequest(IReadOnlyCollection<AttendanceEntryRequest> Entries);

public sealed record MembershipListItemResponse(Guid Id, Guid StudentId, string StudentName, Guid PlanId, string PlanName, MembershipPlanType PlanType, DateOnly StartDate, DateOnly? EndDate, MembershipStatus Status, decimal Price, int? RemainingLessons);
public sealed record CreateMembershipRequest(Guid StudentId, Guid PlanId, DateOnly StartDate, DateOnly? EndDate, decimal? Price, decimal? DiscountAmount, string? DiscountReason);
public sealed record MembershipPlanResponse(Guid Id, string Name, MembershipPlanType Type, decimal DefaultPrice, int? LessonCount, int? DurationMonths, bool IsActive);
public sealed record MembershipPlanRequest(string Name, MembershipPlanType Type, decimal DefaultPrice, int? LessonCount, int? DurationMonths, bool IsActive = true);

public sealed record InvoiceOptionResponse(Guid Id, string Description, decimal Amount, decimal Paid, decimal Remaining, DateOnly DueDate);
public sealed record PaymentListItemResponse(Guid Id, Guid StudentId, string StudentName, decimal Amount, DateTime PaymentDate, PaymentMethod PaymentMethod, Guid? InvoiceId, string? InvoiceDescription, string? Notes);
public sealed record CreatePaymentRequest(Guid StudentId, Guid? InvoiceId, decimal Amount, PaymentMethod PaymentMethod, DateTime? PaymentDate, string? Notes);
public sealed record BalanceListItemResponse(Guid StudentId, string StudentName, decimal TotalDebt, decimal Paid, decimal Remaining, DateTime? LastPaymentDate);
public sealed record BalanceSummaryResponse(decimal OpenBalance, int DebtorCount, decimal CollectedThisMonth, decimal OverdueTotal);
public sealed record BalancesResponse(BalanceSummaryResponse Summary, IReadOnlyCollection<BalanceListItemResponse> Items);

public sealed record ReportPointResponse(string Label, decimal Value);
public sealed record ReportsResponse(int ActiveStudents, int ActiveClasses, decimal CollectedThisMonth, decimal OpenBalance, decimal AverageOccupancy, decimal AttendanceRate, IReadOnlyCollection<ReportPointResponse> MonthlyCollections, IReadOnlyCollection<ReportPointResponse> StudentStatuses, IReadOnlyCollection<ReportPointResponse> ClassOccupancies);
public sealed record InstructorDetailResponse(Guid Id, string FirstName, string LastName, string? Phone, string? Email, string? UserId, string? LinkedUserName, bool IsArchived, int ActiveClassCount, IReadOnlyCollection<ScheduleItemResponse> Schedule);
public sealed record UserResponse(string Id, string DisplayName, string Email, IReadOnlyCollection<string> Roles, bool IsActive);
public sealed record UpdateUserRequest(string DisplayName, bool IsActive, IReadOnlyCollection<string> Roles);

public interface IOperationsService
{
    Task<IReadOnlyCollection<ScheduleItemResponse>> GetScheduleAsync(Guid? roomId, Guid? instructorId, DayOfWeek? day, Guid? classId, CancellationToken ct);
    Task<IReadOnlyCollection<SessionListItemResponse>> GetSessionsAsync(DateOnly date, string? userId, bool instructorOnly, CancellationToken ct);
    Task<AttendanceDetailResponse?> GetAttendanceAsync(Guid sessionId, string? userId, bool instructorOnly, CancellationToken ct);
    Task<AttendanceDetailResponse?> SaveAttendanceAsync(Guid sessionId, SaveAttendanceRequest request, string userId, bool instructorOnly, CancellationToken ct);
    Task<IReadOnlyCollection<MembershipListItemResponse>> GetMembershipsAsync(string? search, MembershipStatus? status, CancellationToken ct);
    Task<MembershipListItemResponse> CreateMembershipAsync(CreateMembershipRequest request, string userId, CancellationToken ct);
    Task<IReadOnlyCollection<MembershipPlanResponse>> GetPlansAsync(bool activeOnly, CancellationToken ct);
    Task<MembershipPlanResponse> CreatePlanAsync(MembershipPlanRequest request, CancellationToken ct);
    Task<MembershipPlanResponse?> UpdatePlanAsync(Guid id, MembershipPlanRequest request, CancellationToken ct);
    Task<IReadOnlyCollection<PaymentListItemResponse>> GetPaymentsAsync(DateOnly? from, DateOnly? to, string? search, CancellationToken ct);
    Task<IReadOnlyCollection<InvoiceOptionResponse>> GetOpenInvoicesAsync(Guid studentId, CancellationToken ct);
    Task<PaymentListItemResponse> CreatePaymentAsync(CreatePaymentRequest request, string userId, CancellationToken ct);
    Task<BalancesResponse> GetBalancesAsync(string? search, CancellationToken ct);
    Task<ReportsResponse> GetReportsAsync(CancellationToken ct);
    Task<InstructorDetailResponse?> GetInstructorAsync(Guid id, CancellationToken ct);
}
