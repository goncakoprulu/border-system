using Border.Application.Auditing;
using Border.Application.Students;
using Border.Domain.Entities;
using Border.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Border.Infrastructure.Students;

internal sealed class StudentService(BorderDbContext dbContext, IAuditWriter auditWriter) : IStudentService
{
    public async Task<PagedResponse<StudentListItemResponse>> GetStudentsAsync(StudentListQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var students = dbContext.Students.AsNoTracking().AsQueryable();
        if (!query.IncludeArchived) students = students.Where(x => !x.IsDeleted);
        if (query.Status.HasValue) students = students.Where(x => x.Status == query.Status.Value);

        var search = Clean(query.Search);
        if (search is not null)
        {
            var lowered = search.ToLowerInvariant();
            var phone = NormalizePhone(search);
            students = students.Where(x =>
                x.FirstName.ToLower().Contains(lowered) ||
                x.LastName.ToLower().Contains(lowered) ||
                (x.FirstName + " " + x.LastName).ToLower().Contains(lowered) ||
                (x.Email != null && x.Email.ToLower().Contains(lowered)) ||
                (x.Phone != null && (x.Phone.Contains(search) || (phone != null && x.Phone.Contains(phone)))));
        }

        students = (query.SortBy.ToLowerInvariant(), query.SortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase)) switch
        {
            ("registrationdate", true) => students.OrderByDescending(x => x.RegistrationDate).ThenBy(x => x.LastName),
            ("registrationdate", false) => students.OrderBy(x => x.RegistrationDate).ThenBy(x => x.LastName),
            ("status", true) => students.OrderByDescending(x => x.Status).ThenBy(x => x.LastName),
            ("status", false) => students.OrderBy(x => x.Status).ThenBy(x => x.LastName),
            ("createdat", true) => students.OrderByDescending(x => x.CreatedAt),
            ("createdat", false) => students.OrderBy(x => x.CreatedAt),
            (_, true) => students.OrderByDescending(x => x.LastName).ThenByDescending(x => x.FirstName),
            _ => students.OrderBy(x => x.LastName).ThenBy(x => x.FirstName)
        };

        var totalCount = await students.CountAsync(cancellationToken);
        var items = await students.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new StudentListItemResponse(x.Id, x.FirstName, x.LastName, x.Phone, x.Email, x.Status, x.RegistrationDate, x.IsDeleted))
            .ToListAsync(cancellationToken);
        return new(items, page, pageSize, totalCount, (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<StudentDetailResponse?> GetStudentAsync(Guid id, bool includeArchived, CancellationToken cancellationToken)
    {
        var student = await dbContext.Students.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && (includeArchived || !x.IsDeleted), cancellationToken);
        if (student is null) return null;
        var guardians = await dbContext.Guardians.AsNoTracking().Where(x => x.StudentId == id).OrderBy(x => x.LastName).ThenBy(x => x.FirstName)
            .Select(x => new GuardianResponse(x.Id, x.StudentId, x.FirstName, x.LastName, x.Relationship, x.Phone, x.Email))
            .ToListAsync(cancellationToken);
        var enrollments = await dbContext.ClassEnrollments.AsNoTracking().Where(x => x.StudentId == id)
            .OrderByDescending(x => x.StartDate)
            .Select(x => new StudentClassEnrollmentResponse(
                x.Id, x.StudioClassId, x.StudioClass.Name,
                x.StudioClass.Instructor.FirstName + " " + x.StudioClass.Instructor.LastName,
                x.StudioClass.StudioRoom.Name, x.StartDate, x.EndDate, x.Status,
                dbContext.ClassSchedules.Where(s => s.StudioClassId == x.StudioClassId).OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime)
                    .Select(s => new StudentClassScheduleResponse(s.DayOfWeek, s.StartTime, s.EndTime)).ToList()))
            .ToListAsync(cancellationToken);
        return Map(student, guardians, enrollments);
    }

    public async Task<CreateStudentResponse> CreateStudentAsync(StudentUpsertRequest request, CancellationToken cancellationToken)
    {
        var cleaned = Clean(request);
        var duplicateWarnings = await FindDuplicatesAsync(cleaned.Phone, cleaned.Email, null, cancellationToken);
        var student = new Student
        {
            FirstName = cleaned.FirstName,
            LastName = cleaned.LastName,
            Phone = cleaned.Phone,
            Email = cleaned.Email,
            BirthDate = cleaned.BirthDate,
            Gender = cleaned.Gender,
            Notes = cleaned.Notes,
            Status = cleaned.Status,
            RegistrationDate = cleaned.RegistrationDate
        };
        dbContext.Students.Add(student);
        Guardian? guardian = null;
        if (cleaned.Guardian is not null)
        {
            guardian = new Guardian
            {
                StudentId = student.Id,
                FirstName = cleaned.Guardian.FirstName,
                LastName = cleaned.Guardian.LastName,
                Phone = cleaned.Guardian.Phone,
                Relationship = "Veli"
            };
            dbContext.Guardians.Add(guardian);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync("StudentCreated", nameof(Student), student.Id.ToString(), null, new { student.FirstName, student.LastName, student.Status }, cancellationToken);
        if (guardian is not null)
            await auditWriter.WriteAsync("GuardianCreated", nameof(Guardian), guardian.Id.ToString(), null, new { guardian.StudentId, guardian.FirstName, guardian.LastName, guardian.Relationship }, cancellationToken);
        return new(Map(student, guardian is null ? [] : [Map(guardian)], []), duplicateWarnings);
    }

    public async Task<StudentDetailResponse?> UpdateStudentAsync(Guid id, StudentUpsertRequest request, CancellationToken cancellationToken)
    {
        var student = await dbContext.Students.SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (student is null) return null;
        var oldValues = new { student.FirstName, student.LastName, student.Phone, student.Email, student.BirthDate, student.Gender, student.Notes, student.Status, student.RegistrationDate };
        var cleaned = Clean(request);
        student.FirstName = cleaned.FirstName;
        student.LastName = cleaned.LastName;
        student.Phone = cleaned.Phone;
        student.Email = cleaned.Email;
        student.BirthDate = cleaned.BirthDate;
        student.Gender = cleaned.Gender;
        student.Notes = cleaned.Notes;
        student.Status = cleaned.Status;
        student.RegistrationDate = cleaned.RegistrationDate;
        Guardian? changedGuardian = null;
        object? oldGuardianValues = null;
        if (cleaned.Guardian is not null)
        {
            Guardian? guardian = null;
            if (cleaned.Guardian.Id.HasValue)
                guardian = await dbContext.Guardians.SingleOrDefaultAsync(x => x.Id == cleaned.Guardian.Id.Value && x.StudentId == id, cancellationToken);
            if (cleaned.Guardian.Id.HasValue && guardian is null) return null;
            if (guardian is null)
            {
                guardian = new Guardian
                {
                    StudentId = id,
                    FirstName = cleaned.Guardian.FirstName,
                    LastName = cleaned.Guardian.LastName,
                    Phone = cleaned.Guardian.Phone,
                    Relationship = "Veli"
                };
                dbContext.Guardians.Add(guardian);
            }
            else
            {
                oldGuardianValues = new { guardian.FirstName, guardian.LastName, guardian.Phone };
                guardian.FirstName = cleaned.Guardian.FirstName;
                guardian.LastName = cleaned.Guardian.LastName;
                guardian.Phone = cleaned.Guardian.Phone;
            }
            changedGuardian = guardian;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync("StudentUpdated", nameof(Student), student.Id.ToString(), oldValues, new { student.FirstName, student.LastName, student.Phone, student.Email, student.BirthDate, student.Gender, student.Notes, student.Status, student.RegistrationDate }, cancellationToken);
        if (changedGuardian is not null)
            await auditWriter.WriteAsync(oldGuardianValues is null ? "GuardianCreated" : "GuardianUpdated", nameof(Guardian), changedGuardian.Id.ToString(), oldGuardianValues, new { changedGuardian.StudentId, changedGuardian.FirstName, changedGuardian.LastName, changedGuardian.Phone, changedGuardian.Relationship }, cancellationToken);
        return await GetStudentAsync(id, false, cancellationToken);
    }

    public async Task<StudentDetailResponse?> ChangeStatusAsync(Guid id, StudentStatus status, CancellationToken cancellationToken)
    {
        var student = await dbContext.Students.SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (student is null) return null;
        var oldStatus = student.Status;
        student.Status = status;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync("StudentStatusChanged", nameof(Student), id.ToString(), new { Status = oldStatus }, new { Status = status }, cancellationToken);
        return await GetStudentAsync(id, false, cancellationToken);
    }

    public async Task<bool> ArchiveStudentAsync(Guid id, CancellationToken cancellationToken)
    {
        var student = await dbContext.Students.SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (student is null) return false;
        student.IsDeleted = true;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync("StudentArchived", nameof(Student), id.ToString(), new { IsDeleted = false }, new { IsDeleted = true }, cancellationToken);
        return true;
    }

    public async Task<IReadOnlyCollection<GuardianResponse>?> GetGuardiansAsync(Guid studentId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Students.AsNoTracking().AnyAsync(x => x.Id == studentId && !x.IsDeleted, cancellationToken)) return null;
        return await dbContext.Guardians.AsNoTracking().Where(x => x.StudentId == studentId).OrderBy(x => x.LastName).ThenBy(x => x.FirstName)
            .Select(x => new GuardianResponse(x.Id, x.StudentId, x.FirstName, x.LastName, x.Relationship, x.Phone, x.Email))
            .ToListAsync(cancellationToken);
    }

    public async Task<GuardianResponse?> AddGuardianAsync(Guid studentId, GuardianUpsertRequest request, CancellationToken cancellationToken)
    {
        if (!await dbContext.Students.AnyAsync(x => x.Id == studentId && !x.IsDeleted, cancellationToken)) return null;
        var cleaned = Clean(request);
        var guardian = new Guardian { StudentId = studentId, FirstName = cleaned.FirstName, LastName = cleaned.LastName, Relationship = cleaned.Relationship, Phone = cleaned.Phone, Email = cleaned.Email };
        dbContext.Guardians.Add(guardian);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync("GuardianCreated", nameof(Guardian), guardian.Id.ToString(), null, new { guardian.StudentId, guardian.FirstName, guardian.LastName, guardian.Relationship }, cancellationToken);
        return Map(guardian);
    }

    public async Task<GuardianResponse?> UpdateGuardianAsync(Guid studentId, Guid guardianId, GuardianUpsertRequest request, CancellationToken cancellationToken)
    {
        if (!await dbContext.Students.AnyAsync(x => x.Id == studentId && !x.IsDeleted, cancellationToken)) return null;
        var guardian = await dbContext.Guardians.SingleOrDefaultAsync(x => x.Id == guardianId && x.StudentId == studentId, cancellationToken);
        if (guardian is null) return null;
        var oldValues = new { guardian.FirstName, guardian.LastName, guardian.Relationship, guardian.Phone, guardian.Email };
        var cleaned = Clean(request);
        guardian.FirstName = cleaned.FirstName;
        guardian.LastName = cleaned.LastName;
        guardian.Relationship = cleaned.Relationship;
        guardian.Phone = cleaned.Phone;
        guardian.Email = cleaned.Email;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync("GuardianUpdated", nameof(Guardian), guardian.Id.ToString(), oldValues, new { guardian.FirstName, guardian.LastName, guardian.Relationship, guardian.Phone, guardian.Email }, cancellationToken);
        return Map(guardian);
    }

    public async Task<GuardianDeleteResult> DeleteGuardianAsync(Guid studentId, Guid guardianId, CancellationToken cancellationToken)
    {
        var student = await dbContext.Students.Include(x => x.Guardians).SingleOrDefaultAsync(x => x.Id == studentId && !x.IsDeleted, cancellationToken);
        if (student is null) return GuardianDeleteResult.NotFound;
        var guardian = await dbContext.Guardians.SingleOrDefaultAsync(x => x.Id == guardianId && x.StudentId == studentId, cancellationToken);
        if (guardian is null) return GuardianDeleteResult.NotFound;
        if (StudentValidation.IsMinor(student.BirthDate) && student.Guardians.Count <= 1) return GuardianDeleteResult.RequiredForMinor;
        dbContext.Guardians.Remove(guardian);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync("GuardianDeleted", nameof(Guardian), guardian.Id.ToString(), new { guardian.StudentId, guardian.FirstName, guardian.LastName, guardian.Relationship }, null, cancellationToken);
        return GuardianDeleteResult.Deleted;
    }

    private async Task<IReadOnlyCollection<DuplicateStudentResponse>> FindDuplicatesAsync(string? phone, string? email, Guid? excludedId, CancellationToken cancellationToken)
    {
        if (phone is null && email is null) return [];
        return await dbContext.Students.AsNoTracking()
            .Where(x => (!excludedId.HasValue || x.Id != excludedId) && ((phone != null && x.Phone == phone) || (email != null && x.Email == email)))
            .Select(x => new DuplicateStudentResponse(x.Id, x.FirstName + " " + x.LastName, x.Phone, x.Email,
                phone != null && x.Phone == phone && email != null && x.Email == email ? "phone,email" : phone != null && x.Phone == phone ? "phone" : "email"))
            .Take(10).ToListAsync(cancellationToken);
    }

    private static StudentDetailResponse Map(Student student, IReadOnlyCollection<GuardianResponse> guardians, IReadOnlyCollection<StudentClassEnrollmentResponse> enrollments) =>
        new(student.Id, student.FirstName, student.LastName, student.Phone, student.Email, student.BirthDate, student.Gender, student.Notes, student.Status, student.RegistrationDate, student.CreatedAt, student.UpdatedAt, student.IsDeleted, guardians, enrollments);
    private static GuardianResponse Map(Guardian guardian) => new(guardian.Id, guardian.StudentId, guardian.FirstName, guardian.LastName, guardian.Relationship, guardian.Phone, guardian.Email);

    private static StudentUpsertRequest Clean(StudentUpsertRequest request) => request with
    {
        FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(), Phone = NormalizePhone(request.Phone), Email = Clean(request.Email)?.ToLowerInvariant(),
        Gender = Clean(request.Gender), Notes = Clean(request.Notes),
        Guardian = request.Guardian is null ? null : request.Guardian with
        {
            FirstName = request.Guardian.FirstName.Trim(), LastName = request.Guardian.LastName.Trim(), Phone = NormalizePhone(request.Guardian.Phone) ?? string.Empty
        }
    };
    private static GuardianUpsertRequest Clean(GuardianUpsertRequest request) => request with
    {
        FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(), Relationship = request.Relationship.Trim(),
        Phone = NormalizePhone(request.Phone), Email = Clean(request.Email)?.ToLowerInvariant()
    };
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? NormalizePhone(string? value)
    {
        var cleaned = Clean(value);
        if (cleaned is null) return null;
        var normalized = new string(cleaned.Where((character, index) => char.IsDigit(character) || (character == '+' && index == 0)).ToArray());
        return normalized.Length == 0 ? null : normalized;
    }
}
