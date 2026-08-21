using Border.Application.Auditing;
using Border.Application.Auth;
using Border.Application.Classes;
using Border.Application.Students;
using Border.Domain.Entities;
using Border.Infrastructure.Persistence;
using Border.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Border.Infrastructure.Classes;

internal sealed class ClassService(BorderDbContext dbContext, IAuditWriter auditWriter, UserManager<AppUser> userManager) : IClassService
{
    public async Task<PagedResponse<ClassListItemResponse>> GetClassesAsync(ClassListQuery query, ClassAccessScope scope, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var today = Today();
        var classes = Scope(dbContext.StudioClasses.AsNoTracking(), scope);
        if (!query.IncludeArchived) classes = classes.Where(x => !x.IsDeleted);
        if (query.Status.HasValue) classes = classes.Where(x => x.Status == query.Status);
        if (query.InstructorId.HasValue) classes = classes.Where(x => x.InstructorId == query.InstructorId);
        if (query.RoomId.HasValue) classes = classes.Where(x => x.StudioRoomId == query.RoomId);
        var search = Clean(query.Search);
        if (search is not null)
        {
            var lowered = search.ToLowerInvariant();
            classes = classes.Where(x => x.Name.ToLower().Contains(lowered) ||
                (x.Instructor.FirstName + " " + x.Instructor.LastName).ToLower().Contains(lowered) ||
                x.StudioRoom.Name.ToLower().Contains(lowered));
        }
        classes = (query.SortBy.ToLowerInvariant(), query.SortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase)) switch
        {
            ("startdate", true) => classes.OrderByDescending(x => x.StartDate).ThenBy(x => x.Name),
            ("startdate", false) => classes.OrderBy(x => x.StartDate).ThenBy(x => x.Name),
            ("status", true) => classes.OrderByDescending(x => x.Status).ThenBy(x => x.Name),
            ("status", false) => classes.OrderBy(x => x.Status).ThenBy(x => x.Name),
            (_, true) => classes.OrderByDescending(x => x.Name),
            _ => classes.OrderBy(x => x.Name)
        };
        var total = await classes.CountAsync(cancellationToken);
        var items = await classes.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ClassListItemResponse(
                x.Id, x.Name, x.Instructor.FirstName + " " + x.Instructor.LastName, x.StudioRoom.Name, x.Capacity,
                dbContext.ClassEnrollments.Count(e => e.StudioClassId == x.Id && e.Status == EnrollmentStatus.Active && e.StartDate <= today && (e.EndDate == null || e.EndDate >= today)),
                x.Status, x.StartDate, x.IsDeleted,
                dbContext.ClassSchedules.Where(s => s.StudioClassId == x.Id).OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime)
                    .Select(s => new ClassScheduleResponse(s.Id, s.DayOfWeek, s.StartTime, s.EndTime)).ToList()))
            .ToListAsync(cancellationToken);
        return new(items, page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<ClassDetailResponse?> GetClassAsync(Guid id, ClassAccessScope scope, bool includeArchived, CancellationToken cancellationToken)
    {
        var studioClass = await Scope(dbContext.StudioClasses.AsNoTracking(), scope)
            .SingleOrDefaultAsync(x => x.Id == id && (includeArchived || !x.IsDeleted), cancellationToken);
        if (studioClass is null) return null;
        return await MapDetailAsync(studioClass, cancellationToken);
    }

    public async Task<ClassOperationResult<ClassDetailResponse>> CreateClassAsync(StudioClassUpsertRequest request, CancellationToken cancellationToken)
    {
        var relatedError = await ValidateRelationsAsync(request.InstructorId, request.StudioRoomId, cancellationToken);
        if (relatedError is not null) return ClassOperationResult<ClassDetailResponse>.Conflict(relatedError);
        var conflict = await FindScheduleConflictAsync(request.InstructorId, request.StudioRoomId, request.Status, request.Schedules, null, cancellationToken);
        if (conflict is not null) return ClassOperationResult<ClassDetailResponse>.Conflict(conflict);
        var cleaned = Clean(request);
        var studioClass = new StudioClass
        {
            Name = cleaned.Name, Description = cleaned.Description, InstructorId = cleaned.InstructorId, StudioRoomId = cleaned.StudioRoomId,
            Capacity = cleaned.Capacity, Level = cleaned.Level, AgeGroup = cleaned.AgeGroup, Status = cleaned.Status, StartDate = cleaned.StartDate, EndDate = cleaned.EndDate
        };
        dbContext.StudioClasses.Add(studioClass);
        dbContext.ClassSchedules.AddRange(cleaned.Schedules.Select(x => new ClassSchedule { StudioClassId = studioClass.Id, DayOfWeek = x.DayOfWeek, StartTime = x.StartTime, EndTime = x.EndTime }));
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync("ClassCreated", nameof(StudioClass), studioClass.Id.ToString(), null, new { studioClass.Name, studioClass.InstructorId, studioClass.StudioRoomId, studioClass.Capacity, studioClass.Status, Schedules = cleaned.Schedules }, cancellationToken);
        return ClassOperationResult<ClassDetailResponse>.Success((await GetClassAsync(studioClass.Id, new(false, null), false, cancellationToken))!);
    }

    public async Task<ClassOperationResult<ClassDetailResponse>> UpdateClassAsync(Guid id, StudioClassUpsertRequest request, CancellationToken cancellationToken)
    {
        var studioClass = await dbContext.StudioClasses.SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (studioClass is null) return ClassOperationResult<ClassDetailResponse>.NotFound();
        var relatedError = await ValidateRelationsAsync(request.InstructorId, request.StudioRoomId, cancellationToken);
        if (relatedError is not null) return ClassOperationResult<ClassDetailResponse>.Conflict(relatedError);
        var conflict = await FindScheduleConflictAsync(request.InstructorId, request.StudioRoomId, request.Status, request.Schedules, id, cancellationToken);
        if (conflict is not null) return ClassOperationResult<ClassDetailResponse>.Conflict(conflict);
        var activeCount = await ActiveEnrollmentCountAsync(id, cancellationToken);
        if (request.Capacity < activeCount) return ClassOperationResult<ClassDetailResponse>.Conflict($"Kapasite, mevcut {activeCount} aktif öğrenciden düşük olamaz.");
        var oldValues = new { studioClass.Name, studioClass.InstructorId, studioClass.StudioRoomId, studioClass.Capacity, studioClass.Status };
        var cleaned = Clean(request);
        studioClass.Name = cleaned.Name; studioClass.Description = cleaned.Description; studioClass.InstructorId = cleaned.InstructorId; studioClass.StudioRoomId = cleaned.StudioRoomId;
        studioClass.Capacity = cleaned.Capacity; studioClass.Level = cleaned.Level; studioClass.AgeGroup = cleaned.AgeGroup; studioClass.Status = cleaned.Status; studioClass.StartDate = cleaned.StartDate; studioClass.EndDate = cleaned.EndDate;
        var existingSchedules = await dbContext.ClassSchedules.Where(x => x.StudioClassId == id).ToListAsync(cancellationToken);
        dbContext.ClassSchedules.RemoveRange(existingSchedules);
        dbContext.ClassSchedules.AddRange(cleaned.Schedules.Select(x => new ClassSchedule { StudioClassId = id, DayOfWeek = x.DayOfWeek, StartTime = x.StartTime, EndTime = x.EndTime }));
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync("ClassUpdated", nameof(StudioClass), id.ToString(), oldValues, new { studioClass.Name, studioClass.InstructorId, studioClass.StudioRoomId, studioClass.Capacity, studioClass.Status, Schedules = cleaned.Schedules }, cancellationToken);
        return ClassOperationResult<ClassDetailResponse>.Success((await GetClassAsync(id, new(false, null), false, cancellationToken))!);
    }

    public async Task<ClassOperationResult<ClassDetailResponse>> ChangeStatusAsync(Guid id, StudioClassStatus status, CancellationToken cancellationToken)
    {
        var studioClass = await dbContext.StudioClasses.SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (studioClass is null) return ClassOperationResult<ClassDetailResponse>.NotFound();
        if (status == StudioClassStatus.Active)
        {
            var schedules = await dbContext.ClassSchedules.Where(x => x.StudioClassId == id).Select(x => new ClassScheduleRequest(x.DayOfWeek, x.StartTime, x.EndTime)).ToListAsync(cancellationToken);
            var conflict = await FindScheduleConflictAsync(studioClass.InstructorId, studioClass.StudioRoomId, status, schedules, id, cancellationToken);
            if (conflict is not null) return ClassOperationResult<ClassDetailResponse>.Conflict(conflict);
        }
        var old = studioClass.Status;
        studioClass.Status = status;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync("ClassStatusChanged", nameof(StudioClass), id.ToString(), new { Status = old }, new { Status = status }, cancellationToken);
        return ClassOperationResult<ClassDetailResponse>.Success((await GetClassAsync(id, new(false, null), false, cancellationToken))!);
    }

    public async Task<bool> ArchiveClassAsync(Guid id, CancellationToken cancellationToken)
    {
        var studioClass = await dbContext.StudioClasses.SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (studioClass is null) return false;
        studioClass.IsDeleted = true;
        studioClass.Status = StudioClassStatus.Cancelled;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync("ClassArchived", nameof(StudioClass), id.ToString(), new { IsDeleted = false }, new { IsDeleted = true }, cancellationToken);
        return true;
    }

    public async Task<ClassOperationResult<ClassScheduleResponse>> AddScheduleAsync(Guid classId, ClassScheduleRequest request, CancellationToken cancellationToken)
    {
        var studioClass = await dbContext.StudioClasses.SingleOrDefaultAsync(x => x.Id == classId && !x.IsDeleted, cancellationToken);
        if (studioClass is null) return ClassOperationResult<ClassScheduleResponse>.NotFound();
        if (await dbContext.ClassSchedules.AnyAsync(x => x.StudioClassId == classId && x.DayOfWeek == request.DayOfWeek && x.StartTime == request.StartTime && x.EndTime == request.EndTime, cancellationToken)) return ClassOperationResult<ClassScheduleResponse>.Conflict("Aynı program satırı zaten mevcut.");
        var conflict = await FindScheduleConflictAsync(studioClass.InstructorId, studioClass.StudioRoomId, studioClass.Status, [request], classId, cancellationToken);
        if (conflict is not null) return ClassOperationResult<ClassScheduleResponse>.Conflict(conflict);
        var schedule = new ClassSchedule { StudioClassId = classId, DayOfWeek = request.DayOfWeek, StartTime = request.StartTime, EndTime = request.EndTime };
        dbContext.ClassSchedules.Add(schedule);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync("ClassScheduleCreated", nameof(ClassSchedule), schedule.Id.ToString(), null, request, cancellationToken);
        return ClassOperationResult<ClassScheduleResponse>.Success(Map(schedule));
    }

    public async Task<ClassOperationResult<ClassScheduleResponse>> UpdateScheduleAsync(Guid classId, Guid scheduleId, ClassScheduleRequest request, CancellationToken cancellationToken)
    {
        var studioClass = await dbContext.StudioClasses.SingleOrDefaultAsync(x => x.Id == classId && !x.IsDeleted, cancellationToken);
        if (studioClass is null) return ClassOperationResult<ClassScheduleResponse>.NotFound();
        var schedule = await dbContext.ClassSchedules.SingleOrDefaultAsync(x => x.Id == scheduleId && x.StudioClassId == classId, cancellationToken);
        if (schedule is null) return ClassOperationResult<ClassScheduleResponse>.NotFound();
        if (await dbContext.ClassSchedules.AnyAsync(x => x.Id != scheduleId && x.StudioClassId == classId && x.DayOfWeek == request.DayOfWeek && x.StartTime == request.StartTime && x.EndTime == request.EndTime, cancellationToken)) return ClassOperationResult<ClassScheduleResponse>.Conflict("Aynı program satırı zaten mevcut.");
        var conflict = await FindScheduleConflictAsync(studioClass.InstructorId, studioClass.StudioRoomId, studioClass.Status, [request], classId, cancellationToken);
        if (conflict is not null) return ClassOperationResult<ClassScheduleResponse>.Conflict(conflict);
        var old = new { schedule.DayOfWeek, schedule.StartTime, schedule.EndTime };
        schedule.DayOfWeek = request.DayOfWeek; schedule.StartTime = request.StartTime; schedule.EndTime = request.EndTime;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync("ClassScheduleUpdated", nameof(ClassSchedule), schedule.Id.ToString(), old, request, cancellationToken);
        return ClassOperationResult<ClassScheduleResponse>.Success(Map(schedule));
    }

    public async Task<bool?> DeleteScheduleAsync(Guid classId, Guid scheduleId, CancellationToken cancellationToken)
    {
        if (!await dbContext.StudioClasses.AnyAsync(x => x.Id == classId && !x.IsDeleted, cancellationToken)) return null;
        var schedule = await dbContext.ClassSchedules.SingleOrDefaultAsync(x => x.Id == scheduleId && x.StudioClassId == classId, cancellationToken);
        if (schedule is null) return false;
        dbContext.ClassSchedules.Remove(schedule);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync("ClassScheduleDeleted", nameof(ClassSchedule), schedule.Id.ToString(), new { schedule.DayOfWeek, schedule.StartTime, schedule.EndTime }, null, cancellationToken);
        return true;
    }

    public async Task<ClassOperationResult<ClassEnrollmentResponse>> EnrollStudentAsync(Guid classId, CreateEnrollmentRequest request, CancellationToken cancellationToken)
    {
        var studioClass = await dbContext.StudioClasses.SingleOrDefaultAsync(x => x.Id == classId && !x.IsDeleted, cancellationToken);
        if (studioClass is null || !await dbContext.Students.AnyAsync(x => x.Id == request.StudentId && !x.IsDeleted, cancellationToken)) return ClassOperationResult<ClassEnrollmentResponse>.NotFound();
        var overlaps = await dbContext.ClassEnrollments.AnyAsync(x => x.StudioClassId == classId && x.StudentId == request.StudentId && x.Status == EnrollmentStatus.Active && (x.EndDate == null || x.EndDate >= request.StartDate), cancellationToken);
        if (overlaps) return ClassOperationResult<ClassEnrollmentResponse>.Conflict("Öğrencinin bu sınıfta çakışan aktif bir kaydı bulunuyor.");
        if (await ActiveEnrollmentCountAsync(classId, cancellationToken) >= studioClass.Capacity) return ClassOperationResult<ClassEnrollmentResponse>.Conflict($"Sınıf kapasitesi dolu ({studioClass.Capacity}/{studioClass.Capacity}).");
        var enrollment = new ClassEnrollment { StudioClassId = classId, StudentId = request.StudentId, StartDate = request.StartDate, Status = EnrollmentStatus.Active };
        dbContext.ClassEnrollments.Add(enrollment);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync("StudentEnrolled", nameof(ClassEnrollment), enrollment.Id.ToString(), null, new { enrollment.StudentId, enrollment.StudioClassId, enrollment.StartDate }, cancellationToken);
        return ClassOperationResult<ClassEnrollmentResponse>.Success((await MapEnrollmentAsync(enrollment.Id, cancellationToken))!);
    }

    public async Task<ClassOperationResult<ClassEnrollmentResponse>> EndEnrollmentAsync(Guid classId, Guid enrollmentId, EndEnrollmentRequest request, CancellationToken cancellationToken)
    {
        var enrollment = await dbContext.ClassEnrollments.SingleOrDefaultAsync(x => x.Id == enrollmentId && x.StudioClassId == classId, cancellationToken);
        if (enrollment is null) return ClassOperationResult<ClassEnrollmentResponse>.NotFound();
        if (enrollment.Status != EnrollmentStatus.Active) return ClassOperationResult<ClassEnrollmentResponse>.Conflict("Yalnızca aktif bir sınıf kaydı sonlandırılabilir.");
        var endDate = request.EndDate ?? Today();
        if (endDate < enrollment.StartDate) return ClassOperationResult<ClassEnrollmentResponse>.Conflict("Bitiş tarihi kayıt başlangıcından önce olamaz.");
        enrollment.EndDate = endDate;
        enrollment.Status = EnrollmentStatus.Completed;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync("EnrollmentEnded", nameof(ClassEnrollment), enrollment.Id.ToString(), new { EndDate = (DateOnly?)null, Status = EnrollmentStatus.Active }, new { enrollment.EndDate, enrollment.Status }, cancellationToken);
        return ClassOperationResult<ClassEnrollmentResponse>.Success((await MapEnrollmentAsync(enrollment.Id, cancellationToken))!);
    }

    public async Task<IReadOnlyCollection<InstructorOptionResponse>> GetInstructorOptionsAsync(CancellationToken cancellationToken) =>
        await dbContext.Instructors.AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.LastName).ThenBy(x => x.FirstName)
            .Select(x => new InstructorOptionResponse(x.Id, x.FirstName + " " + x.LastName, x.UserId)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<InstructorResponse>> GetInstructorsAsync(CancellationToken cancellationToken) =>
        await dbContext.Instructors.AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.LastName).ThenBy(x => x.FirstName)
            .Select(x => new InstructorResponse(x.Id, x.FirstName, x.LastName, x.Phone, x.Email, x.UserId, x.IsDeleted)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<InstructorLoginOptionResponse>> GetInstructorLoginOptionsAsync(CancellationToken cancellationToken)
    {
        var users = await userManager.GetUsersInRoleAsync(Roles.Instructor);
        var links = await dbContext.Instructors.AsNoTracking().Where(x => x.UserId != null).ToDictionaryAsync(x => x.UserId!, x => x.Id, cancellationToken);
        return users.OrderBy(x => x.DisplayName).Select(x => new InstructorLoginOptionResponse(x.Id, x.DisplayName, x.Email ?? x.UserName ?? "", links.GetValueOrDefault(x.Id))).ToList();
    }

    public async Task<ClassOperationResult<InstructorResponse>> CreateInstructorAsync(InstructorUpsertRequest request, CancellationToken cancellationToken)
    {
        var linkError = await ValidateInstructorUserAsync(request.UserId, null, cancellationToken);
        if (linkError is not null) return ClassOperationResult<InstructorResponse>.Conflict(linkError);
        var instructor = new Instructor { FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(), Phone = Clean(request.Phone), Email = Clean(request.Email)?.ToLowerInvariant(), UserId = Clean(request.UserId) };
        dbContext.Instructors.Add(instructor);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync("InstructorCreated", nameof(Instructor), instructor.Id.ToString(), null, new { instructor.FirstName, instructor.LastName, instructor.UserId }, cancellationToken);
        return ClassOperationResult<InstructorResponse>.Success(Map(instructor));
    }

    public async Task<ClassOperationResult<InstructorResponse>> UpdateInstructorAsync(Guid id, InstructorUpsertRequest request, CancellationToken cancellationToken)
    {
        var instructor = await dbContext.Instructors.SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (instructor is null) return ClassOperationResult<InstructorResponse>.NotFound();
        var linkError = await ValidateInstructorUserAsync(request.UserId, id, cancellationToken);
        if (linkError is not null) return ClassOperationResult<InstructorResponse>.Conflict(linkError);
        var old = new { instructor.FirstName, instructor.LastName, instructor.UserId };
        instructor.FirstName = request.FirstName.Trim(); instructor.LastName = request.LastName.Trim(); instructor.Phone = Clean(request.Phone); instructor.Email = Clean(request.Email)?.ToLowerInvariant(); instructor.UserId = Clean(request.UserId);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync("InstructorUpdated", nameof(Instructor), id.ToString(), old, new { instructor.FirstName, instructor.LastName, instructor.UserId }, cancellationToken);
        return ClassOperationResult<InstructorResponse>.Success(Map(instructor));
    }

    public async Task<IReadOnlyCollection<StudioRoomResponse>> GetRoomsAsync(bool includeArchived, CancellationToken cancellationToken) =>
        await dbContext.StudioRooms.AsNoTracking().Where(x => includeArchived || !x.IsDeleted).OrderBy(x => x.Name)
            .Select(x => new StudioRoomResponse(x.Id, x.Name, x.Description, x.Capacity, x.IsActive, x.IsDeleted)).ToListAsync(cancellationToken);

    public async Task<StudioRoomResponse> CreateRoomAsync(StudioRoomUpsertRequest request, CancellationToken cancellationToken)
    {
        var room = new StudioRoom { Name = request.Name.Trim(), Description = Clean(request.Description), Capacity = request.Capacity, IsActive = request.IsActive };
        dbContext.StudioRooms.Add(room);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync("StudioRoomCreated", nameof(StudioRoom), room.Id.ToString(), null, new { room.Name, room.Capacity, room.IsActive }, cancellationToken);
        return Map(room);
    }

    public async Task<StudioRoomResponse?> UpdateRoomAsync(Guid id, StudioRoomUpsertRequest request, CancellationToken cancellationToken)
    {
        var room = await dbContext.StudioRooms.SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (room is null) return null;
        var old = new { room.Name, room.Description, room.Capacity, room.IsActive };
        room.Name = request.Name.Trim(); room.Description = Clean(request.Description); room.Capacity = request.Capacity; room.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync("StudioRoomUpdated", nameof(StudioRoom), id.ToString(), old, new { room.Name, room.Description, room.Capacity, room.IsActive }, cancellationToken);
        return Map(room);
    }

    public async Task<bool> ArchiveRoomAsync(Guid id, CancellationToken cancellationToken)
    {
        var room = await dbContext.StudioRooms.SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (room is null) return false;
        room.IsDeleted = true; room.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync("StudioRoomArchived", nameof(StudioRoom), id.ToString(), new { IsDeleted = false }, new { IsDeleted = true }, cancellationToken);
        return true;
    }

    private IQueryable<StudioClass> Scope(IQueryable<StudioClass> query, ClassAccessScope scope) =>
        scope.InstructorOnly ? query.Where(x => scope.UserId != null && x.Instructor.UserId == scope.UserId) : query;

    private async Task<ClassDetailResponse> MapDetailAsync(StudioClass studioClass, CancellationToken cancellationToken)
    {
        var instructorName = await dbContext.Instructors.AsNoTracking().Where(x => x.Id == studioClass.InstructorId).Select(x => x.FirstName + " " + x.LastName).SingleAsync(cancellationToken);
        var roomName = await dbContext.StudioRooms.AsNoTracking().Where(x => x.Id == studioClass.StudioRoomId).Select(x => x.Name).SingleAsync(cancellationToken);
        var schedules = await dbContext.ClassSchedules.AsNoTracking().Where(x => x.StudioClassId == studioClass.Id).OrderBy(x => x.DayOfWeek).ThenBy(x => x.StartTime).Select(x => new ClassScheduleResponse(x.Id, x.DayOfWeek, x.StartTime, x.EndTime)).ToListAsync(cancellationToken);
        var enrollments = await dbContext.ClassEnrollments.AsNoTracking().Where(x => x.StudioClassId == studioClass.Id).OrderByDescending(x => x.Status == EnrollmentStatus.Active).ThenBy(x => x.Student.LastName)
            .Select(x => new ClassEnrollmentResponse(x.Id, x.StudentId, x.Student.FirstName + " " + x.Student.LastName, x.Student.Phone, x.Student.Status, x.StartDate, x.EndDate, x.Status)).ToListAsync(cancellationToken);
        return new(studioClass.Id, studioClass.Name, studioClass.Description, studioClass.InstructorId, instructorName, studioClass.StudioRoomId, roomName, studioClass.Capacity, studioClass.Level, studioClass.AgeGroup, studioClass.Status, studioClass.StartDate, studioClass.EndDate, studioClass.IsDeleted, schedules, enrollments);
    }

    private async Task<ClassEnrollmentResponse?> MapEnrollmentAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.ClassEnrollments.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new ClassEnrollmentResponse(x.Id, x.StudentId, x.Student.FirstName + " " + x.Student.LastName, x.Student.Phone, x.Student.Status, x.StartDate, x.EndDate, x.Status)).SingleOrDefaultAsync(cancellationToken);

    private async Task<int> ActiveEnrollmentCountAsync(Guid classId, CancellationToken cancellationToken)
    {
        var today = Today();
        return await dbContext.ClassEnrollments.CountAsync(x => x.StudioClassId == classId && x.Status == EnrollmentStatus.Active && x.StartDate <= today && (x.EndDate == null || x.EndDate >= today), cancellationToken);
    }

    private async Task<string?> ValidateRelationsAsync(Guid instructorId, Guid roomId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Instructors.AnyAsync(x => x.Id == instructorId && !x.IsDeleted, cancellationToken)) return "Seçilen eğitmen bulunamadı veya aktif değil.";
        if (!await dbContext.StudioRooms.AnyAsync(x => x.Id == roomId && !x.IsDeleted && x.IsActive, cancellationToken)) return "Seçilen stüdyo bulunamadı veya aktif değil.";
        return null;
    }

    private async Task<string?> ValidateInstructorUserAsync(string? userId, Guid? instructorId, CancellationToken cancellationToken)
    {
        userId = Clean(userId);
        if (userId is null) return null;
        var user = await userManager.FindByIdAsync(userId);
        if (user is null || !await userManager.IsInRoleAsync(user, Roles.Instructor)) return "Seçilen kullanıcı bulunamadı veya Instructor rolüne sahip değil.";
        if (await dbContext.Instructors.AnyAsync(x => x.UserId == userId && (!instructorId.HasValue || x.Id != instructorId), cancellationToken)) return "Bu kullanıcı başka bir eğitmenle zaten bağlantılı.";
        return null;
    }

    private async Task<string?> FindScheduleConflictAsync(Guid instructorId, Guid roomId, StudioClassStatus status, IReadOnlyCollection<ClassScheduleRequest> requested, Guid? excludedClassId, CancellationToken cancellationToken)
    {
        if (status != StudioClassStatus.Active || requested.Count == 0) return null;
        var days = requested.Select(x => x.DayOfWeek).Distinct().ToArray();
        var candidates = await dbContext.ClassSchedules.AsNoTracking()
            .Where(x => days.Contains(x.DayOfWeek) && x.StudioClass.Status == StudioClassStatus.Active && !x.StudioClass.IsDeleted && (!excludedClassId.HasValue || x.StudioClassId != excludedClassId) && (x.StudioClass.InstructorId == instructorId || x.StudioClass.StudioRoomId == roomId))
            .Select(x => new { x.DayOfWeek, x.StartTime, x.EndTime, x.StudioClass.InstructorId, InstructorName = x.StudioClass.Instructor.FirstName + " " + x.StudioClass.Instructor.LastName, x.StudioClass.StudioRoomId, RoomName = x.StudioClass.StudioRoom.Name })
            .ToListAsync(cancellationToken);
        foreach (var item in requested)
        {
            var overlap = candidates.FirstOrDefault(x => x.DayOfWeek == item.DayOfWeek && x.StartTime < item.EndTime && item.StartTime < x.EndTime);
            if (overlap is null) continue;
            var day = TurkishDay(item.DayOfWeek);
            if (overlap.InstructorId == instructorId) return $"{overlap.InstructorName} eğitmenin {day} {item.StartTime:HH\\:mm}–{item.EndTime:HH\\:mm} saatlerinde başka bir dersi bulunuyor.";
            if (overlap.StudioRoomId == roomId) return $"{overlap.RoomName}, {day} {item.StartTime:HH\\:mm}–{item.EndTime:HH\\:mm} saatlerinde başka bir sınıf tarafından kullanılıyor.";
        }
        return null;
    }

    private static ClassScheduleResponse Map(ClassSchedule x) => new(x.Id, x.DayOfWeek, x.StartTime, x.EndTime);
    private static StudioRoomResponse Map(StudioRoom x) => new(x.Id, x.Name, x.Description, x.Capacity, x.IsActive, x.IsDeleted);
    private static InstructorResponse Map(Instructor x) => new(x.Id, x.FirstName, x.LastName, x.Phone, x.Email, x.UserId, x.IsDeleted);
    private static StudioClassUpsertRequest Clean(StudioClassUpsertRequest request) => request with { Name = request.Name.Trim(), Description = Clean(request.Description), Level = Clean(request.Level), AgeGroup = Clean(request.AgeGroup) };
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
    private static string TurkishDay(DayOfWeek day) => day switch { DayOfWeek.Monday => "Pazartesi", DayOfWeek.Tuesday => "Salı", DayOfWeek.Wednesday => "Çarşamba", DayOfWeek.Thursday => "Perşembe", DayOfWeek.Friday => "Cuma", DayOfWeek.Saturday => "Cumartesi", _ => "Pazar" };
}
