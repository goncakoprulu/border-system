using Border.Domain.Entities;
using Border.Application.Students;

namespace Border.Application.Classes;

public sealed record ClassAccessScope(bool InstructorOnly, string? UserId);
public sealed record ClassListQuery(string? Search = null, StudioClassStatus? Status = null, Guid? InstructorId = null, Guid? RoomId = null, int Page = 1, int PageSize = 20, string SortBy = "name", string SortDirection = "asc", bool IncludeArchived = false);
public sealed record ClassScheduleResponse(Guid Id, DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);
public sealed record ClassScheduleRequest(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);
public sealed record ClassListItemResponse(Guid Id, string Name, string InstructorName, string RoomName, int Capacity, int ActiveStudentCount, StudioClassStatus Status, DateOnly StartDate, bool IsArchived, IReadOnlyCollection<ClassScheduleResponse> Schedules);
public sealed record ClassDetailResponse(Guid Id, string Name, string? Description, Guid InstructorId, string InstructorName, Guid StudioRoomId, string RoomName, int Capacity, string? Level, string? AgeGroup, StudioClassStatus Status, DateOnly StartDate, DateOnly? EndDate, bool IsArchived, IReadOnlyCollection<ClassScheduleResponse> Schedules, IReadOnlyCollection<ClassEnrollmentResponse> Enrollments);
public sealed record StudioClassUpsertRequest(string Name, string? Description, Guid InstructorId, Guid StudioRoomId, int Capacity, string? Level, string? AgeGroup, StudioClassStatus Status, DateOnly StartDate, DateOnly? EndDate, IReadOnlyCollection<ClassScheduleRequest> Schedules);
public sealed record ChangeClassStatusRequest(StudioClassStatus Status);
public sealed record InstructorOptionResponse(Guid Id, string FullName, string? UserId);
public sealed record InstructorResponse(Guid Id, string FirstName, string LastName, string? Phone, string? Email, string? UserId, bool IsArchived);
public sealed record InstructorLoginOptionResponse(string UserId, string DisplayName, string Email, Guid? LinkedInstructorId);
public sealed record InstructorUpsertRequest(string FirstName, string LastName, string? Phone, string? Email, string? UserId);
public sealed record StudioRoomResponse(Guid Id, string Name, string? Description, int? Capacity, bool IsActive, bool IsArchived);
public sealed record StudioRoomUpsertRequest(string Name, string? Description, int? Capacity, bool IsActive = true);
public sealed record ClassEnrollmentResponse(Guid Id, Guid StudentId, string StudentName, string? Phone, StudentStatus StudentStatus, DateOnly StartDate, DateOnly? EndDate, EnrollmentStatus Status);
public sealed record CreateEnrollmentRequest(Guid StudentId, DateOnly StartDate);
public sealed record EndEnrollmentRequest(DateOnly? EndDate);

public sealed record ClassOperationResult<T>(T? Value, string? Error = null, bool IsConflict = false)
{
    public static ClassOperationResult<T> Success(T value) => new(value);
    public static ClassOperationResult<T> NotFound() => new(default!);
    public static ClassOperationResult<T> Conflict(string error) => new(default!, error, true);
}

public interface IClassService
{
    Task<PagedResponse<ClassListItemResponse>> GetClassesAsync(ClassListQuery query, ClassAccessScope scope, CancellationToken cancellationToken);
    Task<ClassDetailResponse?> GetClassAsync(Guid id, ClassAccessScope scope, bool includeArchived, CancellationToken cancellationToken);
    Task<ClassOperationResult<ClassDetailResponse>> CreateClassAsync(StudioClassUpsertRequest request, CancellationToken cancellationToken);
    Task<ClassOperationResult<ClassDetailResponse>> UpdateClassAsync(Guid id, StudioClassUpsertRequest request, CancellationToken cancellationToken);
    Task<ClassOperationResult<ClassDetailResponse>> ChangeStatusAsync(Guid id, StudioClassStatus status, CancellationToken cancellationToken);
    Task<bool> ArchiveClassAsync(Guid id, CancellationToken cancellationToken);
    Task<ClassOperationResult<ClassScheduleResponse>> AddScheduleAsync(Guid classId, ClassScheduleRequest request, CancellationToken cancellationToken);
    Task<ClassOperationResult<ClassScheduleResponse>> UpdateScheduleAsync(Guid classId, Guid scheduleId, ClassScheduleRequest request, CancellationToken cancellationToken);
    Task<bool?> DeleteScheduleAsync(Guid classId, Guid scheduleId, CancellationToken cancellationToken);
    Task<ClassOperationResult<ClassEnrollmentResponse>> EnrollStudentAsync(Guid classId, CreateEnrollmentRequest request, CancellationToken cancellationToken);
    Task<ClassOperationResult<ClassEnrollmentResponse>> EndEnrollmentAsync(Guid classId, Guid enrollmentId, EndEnrollmentRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<InstructorOptionResponse>> GetInstructorOptionsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<InstructorResponse>> GetInstructorsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<InstructorLoginOptionResponse>> GetInstructorLoginOptionsAsync(CancellationToken cancellationToken);
    Task<ClassOperationResult<InstructorResponse>> CreateInstructorAsync(InstructorUpsertRequest request, CancellationToken cancellationToken);
    Task<ClassOperationResult<InstructorResponse>> UpdateInstructorAsync(Guid id, InstructorUpsertRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<StudioRoomResponse>> GetRoomsAsync(bool includeArchived, CancellationToken cancellationToken);
    Task<StudioRoomResponse> CreateRoomAsync(StudioRoomUpsertRequest request, CancellationToken cancellationToken);
    Task<StudioRoomResponse?> UpdateRoomAsync(Guid id, StudioRoomUpsertRequest request, CancellationToken cancellationToken);
    Task<bool> ArchiveRoomAsync(Guid id, CancellationToken cancellationToken);
}
