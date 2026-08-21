using Border.Domain.Entities;

namespace Border.Application.Students;

public sealed record StudentListQuery(
    string? Search = null,
    StudentStatus? Status = null,
    int Page = 1,
    int PageSize = 20,
    string SortBy = "name",
    string SortDirection = "asc",
    bool IncludeArchived = false);

public sealed record PagedResponse<T>(
    IReadOnlyCollection<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record StudentListItemResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string? Phone,
    string? Email,
    StudentStatus Status,
    DateOnly RegistrationDate,
    bool IsArchived);

public sealed record GuardianResponse(
    Guid Id,
    Guid StudentId,
    string FirstName,
    string LastName,
    string Relationship,
    string? Phone,
    string? Email);

public sealed record StudentDetailResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string? Phone,
    string? Email,
    DateOnly? BirthDate,
    string? Gender,
    string? Notes,
    StudentStatus Status,
    DateOnly RegistrationDate,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsArchived,
    IReadOnlyCollection<GuardianResponse> Guardians,
    IReadOnlyCollection<StudentClassEnrollmentResponse> ClassEnrollments);

public sealed record StudentClassScheduleResponse(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);
public sealed record StudentClassEnrollmentResponse(
    Guid EnrollmentId,
    Guid ClassId,
    string ClassName,
    string InstructorName,
    string RoomName,
    DateOnly StartDate,
    DateOnly? EndDate,
    EnrollmentStatus Status,
    IReadOnlyCollection<StudentClassScheduleResponse> Schedules);

public sealed record StudentUpsertRequest(
    string FirstName,
    string LastName,
    string? Phone,
    string? Email,
    DateOnly? BirthDate,
    string? Gender,
    string? Notes,
    StudentStatus Status,
    DateOnly RegistrationDate);

public sealed record ChangeStudentStatusRequest(StudentStatus Status);

public sealed record GuardianUpsertRequest(
    string FirstName,
    string LastName,
    string Relationship,
    string? Phone,
    string? Email);

public sealed record DuplicateStudentResponse(Guid Id, string FullName, string? Phone, string? Email, string MatchedOn);
public sealed record CreateStudentResponse(StudentDetailResponse Student, IReadOnlyCollection<DuplicateStudentResponse> DuplicateWarnings);

public interface IStudentService
{
    Task<PagedResponse<StudentListItemResponse>> GetStudentsAsync(StudentListQuery query, CancellationToken cancellationToken);
    Task<StudentDetailResponse?> GetStudentAsync(Guid id, bool includeArchived, CancellationToken cancellationToken);
    Task<CreateStudentResponse> CreateStudentAsync(StudentUpsertRequest request, CancellationToken cancellationToken);
    Task<StudentDetailResponse?> UpdateStudentAsync(Guid id, StudentUpsertRequest request, CancellationToken cancellationToken);
    Task<StudentDetailResponse?> ChangeStatusAsync(Guid id, StudentStatus status, CancellationToken cancellationToken);
    Task<bool> ArchiveStudentAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<GuardianResponse>?> GetGuardiansAsync(Guid studentId, CancellationToken cancellationToken);
    Task<GuardianResponse?> AddGuardianAsync(Guid studentId, GuardianUpsertRequest request, CancellationToken cancellationToken);
    Task<GuardianResponse?> UpdateGuardianAsync(Guid studentId, Guid guardianId, GuardianUpsertRequest request, CancellationToken cancellationToken);
    Task<bool?> DeleteGuardianAsync(Guid studentId, Guid guardianId, CancellationToken cancellationToken);
}
