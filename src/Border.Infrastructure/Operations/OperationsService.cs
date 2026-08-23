using System.Data;
using Border.Application.Auditing;
using Border.Application.Operations;
using Border.Domain.Entities;
using Border.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Border.Infrastructure.Operations;

internal sealed class OperationsService(BorderDbContext db, IAuditWriter auditWriter) : IOperationsService
{
    private static DateTime IstanbulUtc(DateOnly date, TimeOnly time) =>
        DateTime.SpecifyKind(date.ToDateTime(time).AddHours(-3), DateTimeKind.Utc);

    private static DateTime IstanbulStart(DateOnly date) => IstanbulUtc(date, TimeOnly.MinValue);

    public async Task<IReadOnlyCollection<ScheduleItemResponse>> GetScheduleAsync(Guid? roomId, Guid? instructorId, DayOfWeek? day, Guid? classId, CancellationToken ct)
    {
        var query = db.ClassSchedules.AsNoTracking().Where(x => !x.StudioClass.IsDeleted && x.StudioClass.Status == StudioClassStatus.Active);
        if (roomId.HasValue) query = query.Where(x => x.StudioClass.StudioRoomId == roomId);
        if (instructorId.HasValue) query = query.Where(x => x.StudioClass.InstructorId == instructorId);
        if (day.HasValue) query = query.Where(x => x.DayOfWeek == day);
        if (classId.HasValue) query = query.Where(x => x.StudioClassId == classId);
        return await query.OrderBy(x => x.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)x.DayOfWeek).ThenBy(x => x.StartTime)
            .Select(x => new ScheduleItemResponse(x.StudioClassId, x.StudioClass.Name, x.StudioClass.Instructor.FirstName + " " + x.StudioClass.Instructor.LastName, x.StudioClass.InstructorId, x.StudioClass.StudioRoom.Name, x.StudioClass.StudioRoomId, x.DayOfWeek, x.StartTime, x.EndTime, x.StudioClass.Level)).ToListAsync(ct);
    }

    public async Task<IReadOnlyCollection<SessionListItemResponse>> GetSessionsAsync(DateOnly date, Guid? instructorId, Guid? classId, Guid? roomId, Guid? studentId, string? userId, bool instructorOnly, CancellationToken ct)
    {
        await EnsureSessionsAsync(date, ct);
        var start = IstanbulStart(date); var end = start.AddDays(1);
        var query = db.LessonSessions.AsNoTracking().Where(x => x.ScheduledStart >= start && x.ScheduledStart < end && x.Status != LessonSessionStatus.Cancelled);
        if (instructorOnly) query = query.Where(x => x.Instructor.UserId == userId);
        if (instructorId.HasValue) query = query.Where(x => x.InstructorId == instructorId);
        if (classId.HasValue) query = query.Where(x => x.StudioClassId == classId);
        if (roomId.HasValue) query = query.Where(x => x.StudioRoomId == roomId);
        if (studentId.HasValue) query = query.Where(x => db.Attendances.Any(a => a.LessonSessionId == x.Id && a.StudentId == studentId) || db.ClassEnrollments.Any(e => e.StudioClassId == x.StudioClassId && e.StudentId == studentId && e.Status == EnrollmentStatus.Active && e.StartDate <= date && (e.EndDate == null || e.EndDate >= date)));
        return await query.OrderBy(x => x.ScheduledStart).Select(x => new SessionListItemResponse(x.Id, x.StudioClassId, x.StudioClass.Name, x.InstructorId, x.Instructor.FirstName + " " + x.Instructor.LastName, x.StudioRoomId, x.StudioRoom.Name, x.ScheduledStart, x.ScheduledEnd,
            db.ClassEnrollments.Count(e => e.StudioClassId == x.StudioClassId && e.Status == EnrollmentStatus.Active && e.StartDate <= date && (e.EndDate == null || e.EndDate >= date)),
            db.Attendances.Count(a => a.LessonSessionId == x.Id),
            x.Status == LessonSessionStatus.Completed || db.Attendances.Any(a => a.LessonSessionId == x.Id))).ToListAsync(ct);
    }

    public async Task<DashboardOperationsResponse> GetDashboardOperationsAsync(string? userId, bool instructorOnly, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
        await EnsureSessionsAsync(today, ct);
        var start = IstanbulStart(today); var end = start.AddDays(1);
        var lessonsQuery = db.LessonSessions.AsNoTracking().Where(x => !x.StudioClass.IsDeleted && x.ScheduledStart >= start && x.ScheduledStart < end && x.Status != LessonSessionStatus.Cancelled);
        if (instructorOnly) lessonsQuery = lessonsQuery.Where(x => x.Instructor.UserId == userId);
        var lessons = await lessonsQuery.OrderBy(x => x.ScheduledStart)
            .Select(x => new DashboardLessonResponse(x.Id, x.StudioClassId, x.StudioClass.Name, x.Instructor.FirstName + " " + x.Instructor.LastName, x.StudioRoom.Name, x.ScheduledStart, x.ScheduledEnd,
                db.ClassEnrollments.Count(e => e.StudioClassId == x.StudioClassId && e.Status == EnrollmentStatus.Active && e.StartDate <= today && (e.EndDate == null || e.EndDate >= today) && !e.Student.IsDeleted),
                x.StudioClass.Capacity, x.Status == LessonSessionStatus.Completed || db.Attendances.Any(a => a.LessonSessionId == x.Id)))
            .ToListAsync(ct);
        var activeStudentCount = instructorOnly
            ? await db.ClassEnrollments.AsNoTracking().Where(x => x.Status == EnrollmentStatus.Active && !x.Student.IsDeleted && !x.StudioClass.IsDeleted && x.Student.Status == StudentStatus.Active && x.StudioClass.Instructor.UserId == userId).Select(x => x.StudentId).Distinct().CountAsync(ct)
            : await db.Students.AsNoTracking().CountAsync(x => !x.IsDeleted && x.Status == StudentStatus.Active, ct);
        return new(activeStudentCount, lessons.Count, lessons);
    }

    public async Task<DashboardAnalyticsResponse> GetDashboardAnalyticsAsync(string? userId, bool instructorOnly, bool canViewFinance, CancellationToken ct)
    {
        var nowUtc = DateTime.UtcNow; var today = DateOnly.FromDateTime(nowUtc.AddHours(3));
        var thirtyDayStart = today.AddDays(-29); var startUtc = IstanbulStart(thirtyDayStart); var endUtc = IstanbulStart(today.AddDays(1));
        var attendanceQuery = db.Attendances.AsNoTracking().Where(x => x.LessonSession.ScheduledStart >= startUtc && x.LessonSession.ScheduledStart < endUtc);
        if (instructorOnly) attendanceQuery = attendanceQuery.Where(x => x.LessonSession.Instructor.UserId == userId);
        var attendance = await attendanceQuery.GroupBy(_ => 1).Select(x => new { Total = x.Count(), Attended = x.Count(a => a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late) }).SingleOrDefaultAsync(ct);
        var attendanceRate = attendance is null ? 0 : AttendanceReportingRules.RateFromAttended(attendance.Attended, attendance.Total);
        var newStudentsQuery = db.Students.AsNoTracking().Where(x => !x.IsDeleted && x.RegistrationDate >= thirtyDayStart && x.RegistrationDate <= today);
        if (instructorOnly) newStudentsQuery = newStudentsQuery.Where(x => db.ClassEnrollments.Any(e => e.StudentId == x.Id && e.Status == EnrollmentStatus.Active && !e.StudioClass.IsDeleted && e.StudioClass.Instructor.UserId == userId));
        var newStudents = await newStudentsQuery.CountAsync(ct);
        var activeMemberships = canViewFinance ? await db.StudentMemberships.AsNoTracking().CountAsync(x => !x.Student.IsDeleted && x.Status == MembershipStatus.Active && x.StartDate <= today && (x.EndDate == null || x.EndDate >= today), ct) : 0;
        var payments = canViewFinance ? await db.Payments.AsNoTracking().Where(x => x.PaymentDate >= startUtc && x.PaymentDate < endUtc).Select(x => new { x.PaymentDate, x.Amount }).ToListAsync(ct) : [];
        var dailyRevenue = payments.GroupBy(x => DateOnly.FromDateTime(x.PaymentDate.AddHours(3))).ToDictionary(x => x.Key, x => x.Sum(p => p.Amount));
        var revenuePoints = Enumerable.Range(0, 30).Select(offset => thirtyDayStart.AddDays(offset)).Select(date => new ReportPointResponse(date.ToString("dd.MM"), dailyRevenue.GetValueOrDefault(date))).ToList();
        var monthStart = new DateOnly(today.Year, today.Month, 1); var monthlyRevenue = canViewFinance ? await db.Payments.AsNoTracking().Where(x => x.PaymentDate >= IstanbulStart(monthStart) && x.PaymentDate < endUtc).SumAsync(x => (decimal?)x.Amount, ct) ?? 0 : 0;
        var invoiceRows = canViewFinance ? await db.Invoices.AsNoTracking().Where(x => x.Status != InvoiceStatus.Cancelled && !x.Student.IsDeleted).Select(x => new { x.StudentId, x.Amount, x.DueDate, x.Status, Paid = db.Payments.Where(p => p.InvoiceId == x.Id).Sum(p => (decimal?)p.Amount) ?? 0 }).ToListAsync(ct) : [];
        var outstandingBalance = invoiceRows.Sum(x => InvoiceBalanceRules.Remaining(x.Status, x.Amount, x.Paid));
        var alerts = new List<DashboardAlertResponse>();
        if (canViewFinance)
        {
            var overdue = invoiceRows.Count(x => x.DueDate < today && InvoiceBalanceRules.Remaining(x.Status, x.Amount, x.Paid) > 0); if (overdue > 0) alerts.Add(new("OverdueInvoices", overdue, $"{overdue} gecikmiş ödeme", "/balances"));
            var debtors = invoiceRows.Where(x => InvoiceBalanceRules.Remaining(x.Status, x.Amount, x.Paid) > 0).Select(x => x.StudentId).Distinct().Count(); if (debtors > 0) alerts.Add(new("OpenBalances", debtors, $"{debtors} öğrencinin açık bakiyesi var", "/balances"));
            var expiring = await db.StudentMemberships.AsNoTracking().CountAsync(x => !x.Student.IsDeleted && x.Status == MembershipStatus.Active && x.EndDate >= today && x.EndDate <= today.AddDays(7), ct); if (expiring > 0) alerts.Add(new("ExpiringMemberships", expiring, $"{expiring} üyelik 7 gün içinde bitiyor", "/memberships"));
        }
        var missingQuery = db.LessonSessions.AsNoTracking().Where(x => !x.StudioClass.IsDeleted && x.Status == LessonSessionStatus.Scheduled && x.ScheduledEnd >= startUtc && x.ScheduledEnd < nowUtc && !db.Attendances.Any(a => a.LessonSessionId == x.Id));
        var capacityQuery = db.StudioClasses.AsNoTracking().Where(x => !x.IsDeleted && x.Status == StudioClassStatus.Active && 10 * db.ClassEnrollments.Count(e => e.StudioClassId == x.Id && e.Status == EnrollmentStatus.Active && !e.Student.IsDeleted) >= 9 * x.Capacity);
        var emptyClassQuery = db.StudioClasses.AsNoTracking().Where(x => !x.IsDeleted && x.Status == StudioClassStatus.Active && !db.ClassEnrollments.Any(e => e.StudioClassId == x.Id && e.Status == EnrollmentStatus.Active && !e.Student.IsDeleted));
        var riskAttendanceQuery = db.Attendances.AsNoTracking().Where(x => !x.Student.IsDeleted && x.Student.Status == StudentStatus.Active && !x.LessonSession.StudioClass.IsDeleted && x.LessonSession.ScheduledStart >= IstanbulStart(today.AddDays(-90)) && x.LessonSession.ScheduledStart < endUtc);
        if (instructorOnly) { missingQuery = missingQuery.Where(x => x.Instructor.UserId == userId); capacityQuery = capacityQuery.Where(x => x.Instructor.UserId == userId); emptyClassQuery = emptyClassQuery.Where(x => x.Instructor.UserId == userId); riskAttendanceQuery = riskAttendanceQuery.Where(x => x.LessonSession.Instructor.UserId == userId); }
        var missing = await missingQuery.CountAsync(ct); if (missing > 0) alerts.Add(new("MissingAttendance", missing, $"{missing} geçmiş dersin yoklaması eksik", "/attendance"));
        var nearCapacity = await capacityQuery.CountAsync(ct); if (nearCapacity > 0) alerts.Add(new("NearCapacity", nearCapacity, $"{nearCapacity} sınıf %90 veya üzeri dolu", instructorOnly ? "/my-classes" : "/classes"));
        var emptyClasses = await emptyClassQuery.CountAsync(ct); if (emptyClasses > 0) alerts.Add(new("EmptyClasses", emptyClasses, $"{emptyClasses} aktif sınıfta kayıtlı öğrenci yok", instructorOnly ? "/my-classes" : "/classes"));
        var riskRows = await riskAttendanceQuery.OrderByDescending(x => x.LessonSession.ScheduledStart).Select(x => new { x.StudentId, x.Status }).ToListAsync(ct);
        var lowAttendance = riskRows.GroupBy(x => x.StudentId).Count(group => { var last = group.Take(4).ToList(); return last.Count >= 3 && last.Count(x => x.Status == AttendanceStatus.Absent) >= 2; });
        if (lowAttendance > 0) alerts.Add(new("LowAttendance", lowAttendance, $"{lowAttendance} öğrenci son derslerde sık devamsızlık yaptı", instructorOnly ? "/attendance" : "/reports"));
        if (!instructorOnly)
        {
            var unassigned = await db.Students.AsNoTracking().CountAsync(x => !x.IsDeleted && x.Status == StudentStatus.Active && !db.ClassEnrollments.Any(e => e.StudentId == x.Id && e.Status == EnrollmentStatus.Active), ct);
            if (unassigned > 0) alerts.Add(new("UnassignedStudents", unassigned, $"{unassigned} aktif öğrenci bir sınıfa atanmamış", "/students?status=Active"));
        }
        return new(canViewFinance, monthlyRevenue, outstandingBalance, attendanceRate, newStudents, payments.Sum(x => x.Amount), activeMemberships, alerts, revenuePoints);
    }

    public async Task<AttendanceDetailResponse?> GetAttendanceAsync(Guid sessionId, string? userId, bool instructorOnly, CancellationToken ct)
    {
        var session = await db.LessonSessions.AsNoTracking().Where(x => x.Id == sessionId && x.Status != LessonSessionStatus.Cancelled && (!instructorOnly || x.Instructor.UserId == userId))
            .Select(x => new SessionListItemResponse(x.Id, x.StudioClassId, x.StudioClass.Name, x.InstructorId, x.Instructor.FirstName + " " + x.Instructor.LastName, x.StudioRoomId, x.StudioRoom.Name, x.ScheduledStart, x.ScheduledEnd, 0, db.Attendances.Count(a => a.LessonSessionId == x.Id), x.Status == LessonSessionStatus.Completed || db.Attendances.Any(a => a.LessonSessionId == x.Id))).SingleOrDefaultAsync(ct);
        if (session is null) return null;
        var date = DateOnly.FromDateTime(session.ScheduledStart.AddHours(3));
        var studentRows = await db.ClassEnrollments.AsNoTracking().Where(x => x.StudioClassId == session.ClassId && x.Status == EnrollmentStatus.Active && x.StartDate <= date && (x.EndDate == null || x.EndDate >= date) && !x.Student.IsDeleted)
            .OrderBy(x => x.Student.FirstName).ThenBy(x => x.Student.LastName)
            .Select(x => new { x.StudentId, StudentName = x.Student.FirstName + " " + x.Student.LastName, StudentNotes = x.Student.Notes, Status = db.Attendances.Where(a => a.LessonSessionId == sessionId && a.StudentId == x.StudentId).Select(a => (AttendanceStatus?)a.Status).FirstOrDefault(), Notes = db.Attendances.Where(a => a.LessonSessionId == sessionId && a.StudentId == x.StudentId).Select(a => a.Notes).FirstOrDefault() }).ToListAsync(ct);
        var studentIds = studentRows.Select(x => x.StudentId).ToArray();
        var recentRows = await db.Attendances.AsNoTracking().Where(x => studentIds.Contains(x.StudentId) && x.LessonSession.StudioClassId == session.ClassId && x.LessonSession.ScheduledStart < session.ScheduledStart)
            .OrderByDescending(x => x.LessonSession.ScheduledStart).Select(x => new { x.StudentId, x.Status }).ToListAsync(ct);
        var recent = recentRows.GroupBy(x => x.StudentId).ToDictionary(x => x.Key, x => x.Take(4).ToList());
        var students = studentRows.Select(x => { var history = recent.GetValueOrDefault(x.StudentId) ?? []; return new AttendanceStudentResponse(x.StudentId, x.StudentName, x.Status, x.Notes, x.StudentNotes, history.Count, history.Count(item => item.Status == AttendanceStatus.Absent)); }).ToList();
        return new(session with { StudentCount = students.Count }, students);
    }

    public async Task<AttendanceDetailResponse?> SaveAttendanceAsync(Guid sessionId, SaveAttendanceRequest request, string userId, bool instructorOnly, CancellationToken ct)
    {
        var detail = await GetAttendanceAsync(sessionId, userId, instructorOnly, ct); if (detail is null) return null;
        var allowed = detail.Students.Select(x => x.StudentId).ToHashSet();
        if (request.Entries.Count != request.Entries.Select(x => x.StudentId).Distinct().Count() || request.Entries.Any(x => !allowed.Contains(x.StudentId))) throw new InvalidOperationException("Yoklama listesinde sınıfa kayıtlı olmayan veya yinelenen öğrenci var.");
        if (request.Entries.Count != allowed.Count) throw new InvalidOperationException("Yoklamayı kaydetmeden önce tüm öğrencilerin durumunu seçin.");
        if (request.Entries.Any(x => !Enum.IsDefined(x.Status))) throw new InvalidOperationException("Geçersiz yoklama durumu seçildi.");
        if (request.Entries.Any(x => x.Notes?.Trim().Length > 1000)) throw new InvalidOperationException("Yoklama notu en fazla 1000 karakter olabilir.");
        var existing = await db.Attendances.Where(x => x.LessonSessionId == sessionId).ToDictionaryAsync(x => x.StudentId, ct);
        foreach (var item in request.Entries) { if (existing.TryGetValue(item.StudentId, out var row)) { row.Status = item.Status; row.Notes = Clean(item.Notes); row.UpdatedAt = DateTime.UtcNow; row.RecordedByUserId = userId; } else db.Attendances.Add(new Attendance { LessonSessionId = sessionId, StudentId = item.StudentId, Status = item.Status, Notes = Clean(item.Notes), RecordedByUserId = userId }); }
        var session = await db.LessonSessions.SingleAsync(x => x.Id == sessionId, ct); session.Status = LessonSessionStatus.Completed;
        await db.SaveChangesAsync(ct);
        await auditWriter.WriteAsync("AttendanceSaved", nameof(Attendance), sessionId.ToString(), null, new { SessionId = sessionId, Count = request.Entries.Count, Statuses = request.Entries.GroupBy(x => x.Status).ToDictionary(x => x.Key.ToString(), x => x.Count()) }, ct);
        return await GetAttendanceAsync(sessionId, userId, instructorOnly, ct);
    }

    public async Task<StudentAttendanceHistoryResponse?> GetStudentAttendanceHistoryAsync(Guid studentId, CancellationToken ct)
    {
        if (!await db.Students.AsNoTracking().AnyAsync(x => x.Id == studentId && !x.IsDeleted, ct)) return null;
        var counts = await db.Attendances.AsNoTracking().Where(x => x.StudentId == studentId)
            .GroupBy(_ => 1).Select(group => new
            {
                Total = group.Count(),
                Present = group.Count(x => x.Status == AttendanceStatus.Present),
                Absent = group.Count(x => x.Status == AttendanceStatus.Absent),
                Excused = group.Count(x => x.Status == AttendanceStatus.Excused),
                Late = group.Count(x => x.Status == AttendanceStatus.Late),
                MakeUp = group.Count(x => x.Status == AttendanceStatus.MakeUp),
            }).SingleOrDefaultAsync(ct);
        var items = await db.Attendances.AsNoTracking().Where(x => x.StudentId == studentId)
            .OrderByDescending(x => x.LessonSession.ScheduledStart).Take(10)
            .Select(x => new StudentAttendanceHistoryItemResponse(x.Id, x.LessonSessionId, x.LessonSession.StudioClassId, x.LessonSession.StudioClass.Name, x.LessonSession.ScheduledStart, x.Status, x.Notes))
            .ToListAsync(ct);
        if (counts is null) return new(0, 0, 0, 0, 0, 0, 0, items);
        var rate = counts.Total == 0 ? 0 : Math.Round(100m * (counts.Present + counts.Late) / counts.Total, 1);
        return new(counts.Total, counts.Present, counts.Absent, counts.Excused, counts.Late, counts.MakeUp, rate, items);
    }

    private async Task EnsureSessionsAsync(DateOnly date, CancellationToken ct)
    {
        var schedules = await db.ClassSchedules.AsNoTracking()
            .Where(x => x.DayOfWeek == date.DayOfWeek && !x.StudioClass.IsDeleted && x.StudioClass.Status == StudioClassStatus.Active && x.StudioClass.StartDate <= date && (x.StudioClass.EndDate == null || x.StudioClass.EndDate >= date))
            .Select(x => new { x.StudioClassId, x.StudioClass.InstructorId, x.StudioClass.StudioRoomId, x.StartTime, x.EndTime })
            .ToListAsync(ct);
        if (schedules.Count == 0) return;

        var start = IstanbulStart(date); var end = start.AddDays(1);
        var existing = await db.LessonSessions.AsNoTracking().Where(x => x.ScheduledStart >= start && x.ScheduledStart < end)
            .Select(x => new { x.StudioClassId, x.ScheduledStart }).ToListAsync(ct);
        var keys = existing.Select(x => (x.StudioClassId, x.ScheduledStart)).ToHashSet();
        var missing = schedules.Select(x => new LessonSession
        {
            StudioClassId = x.StudioClassId,
            InstructorId = x.InstructorId,
            StudioRoomId = x.StudioRoomId,
            ScheduledStart = IstanbulUtc(date, x.StartTime),
            ScheduledEnd = IstanbulUtc(date, x.EndTime),
        }).Where(x => keys.Add((x.StudioClassId, x.ScheduledStart))).ToList();
        if (missing.Count == 0) return;
        db.LessonSessions.AddRange(missing);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyCollection<MembershipListItemResponse>> GetMembershipsAsync(string? search, MembershipStatus? status, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
        var q = db.StudentMemberships.AsNoTracking().Where(x => !x.Student.IsDeleted);
        if (status == MembershipStatus.Active) q = q.Where(x => x.Status == MembershipStatus.Active && (x.EndDate == null || x.EndDate >= today));
        else if (status == MembershipStatus.Expired) q = q.Where(x => x.Status == MembershipStatus.Expired || (x.Status == MembershipStatus.Active && x.EndDate < today));
        else if (status.HasValue) q = q.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(search)) { var s = search.Trim().ToLower(); q = q.Where(x => (x.Student.FirstName + " " + x.Student.LastName).ToLower().Contains(s)); }
        var rows = await q.OrderByDescending(x => x.StartDate).Select(x => new { x.Id, x.StudentId, StudentName = x.Student.FirstName + " " + x.Student.LastName, PlanId = x.MembershipPlanId, PlanName = x.MembershipPlan.Name, PlanType = x.MembershipPlan.Type, x.StartDate, x.EndDate, x.Status, Price = db.MembershipPriceHistory.Where(p => p.StudentMembershipId == x.Id).OrderByDescending(p => p.EffectiveFrom).Select(p => p.Price - (p.DiscountAmount ?? 0)).FirstOrDefault(), RemainingLessons = x.MembershipPlan.LessonCount }).ToListAsync(ct);
        return rows.Select(x => new MembershipListItemResponse(x.Id, x.StudentId, x.StudentName, x.PlanId, x.PlanName, x.PlanType, x.StartDate, x.EndDate, x.Status == MembershipStatus.Active && x.EndDate < today ? MembershipStatus.Expired : x.Status, x.Price, x.RemainingLessons)).ToList();
    }

    public async Task<MembershipListItemResponse> CreateMembershipAsync(CreateMembershipRequest request, string userId, CancellationToken ct)
    {
        try
        {
            await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
            var plan = await db.MembershipPlans.SingleOrDefaultAsync(x => x.Id == request.PlanId && x.IsActive, ct) ?? throw new InvalidOperationException("Üyelik planı bulunamadı veya pasif.");
            if (!await db.Students.AnyAsync(x => x.Id == request.StudentId && !x.IsDeleted, ct)) throw new InvalidOperationException("Öğrenci bulunamadı.");
            if (request.StartDate == default || request.EndDate < request.StartDate) throw new InvalidOperationException("Üyelik tarihleri geçersiz.");
            var price = request.Price ?? plan.DefaultPrice; var discount = request.DiscountAmount ?? 0; if (price < 0 || discount < 0 || discount > price) throw new InvalidOperationException("Üyelik ücreti veya indirim geçersiz.");
            var effectiveEnd = request.EndDate ?? (plan.DurationMonths.HasValue ? request.StartDate.AddMonths(plan.DurationMonths.Value) : null);
            var overlaps = await db.StudentMemberships.AnyAsync(x => x.StudentId == request.StudentId && x.MembershipPlanId == request.PlanId && (x.Status == MembershipStatus.Active || x.Status == MembershipStatus.Frozen) && (x.EndDate == null || x.EndDate >= request.StartDate) && (effectiveEnd == null || x.StartDate <= effectiveEnd), ct);
            if (overlaps) throw new InvalidOperationException("Öğrencinin aynı plan için çakışan aktif veya donmuş bir üyeliği bulunuyor.");
            var membership = new StudentMembership { StudentId = request.StudentId, MembershipPlanId = request.PlanId, StartDate = request.StartDate, EndDate = effectiveEnd, Status = MembershipStatus.Active };
            db.StudentMemberships.Add(membership); db.MembershipPriceHistory.Add(new MembershipPriceHistory { StudentMembership = membership, Price = price, DiscountAmount = discount, DiscountReason = Clean(request.DiscountReason), EffectiveFrom = request.StartDate, ApprovedByUserId = userId });
            if (price - discount > 0) db.Invoices.Add(new Invoice { StudentId = request.StudentId, StudentMembership = membership, Description = plan.Name + " üyeliği", Amount = price - discount, DueDate = request.StartDate, Status = InvoiceStatus.Pending });
            await db.SaveChangesAsync(ct);
            await auditWriter.WriteAsync("MembershipCreated", nameof(StudentMembership), membership.Id.ToString(), null, new { membership.StudentId, membership.MembershipPlanId, membership.StartDate, membership.EndDate, Price = price, Discount = discount }, ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return (await GetMembershipsAsync(null, null, ct)).Single(x => x.Id == membership.Id);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            throw new InvalidOperationException("Üyelik bilgisi eşzamanlı olarak değişti. Güncel üyelikleri kontrol edip tekrar deneyin.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure })
        {
            throw new InvalidOperationException("Üyelik bilgisi eşzamanlı olarak değişti. Güncel üyelikleri kontrol edip tekrar deneyin.");
        }
    }

    public async Task<MembershipListItemResponse?> ChangeMembershipStatusAsync(Guid id, ChangeMembershipStatusRequest request, string userId, CancellationToken ct)
    {
        try
        {
            await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
            var membership = await db.StudentMemberships.SingleOrDefaultAsync(x => x.Id == id && !x.Student.IsDeleted, ct); if (membership is null) return null;
            if (!Enum.IsDefined(request.Status)) throw new InvalidOperationException("Geçersiz üyelik durumu seçildi.");
            var old = new { membership.Status, membership.EndDate };
            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
            if (request.Status == MembershipStatus.Frozen && membership.Status != MembershipStatus.Active) throw new InvalidOperationException("Yalnızca aktif üyelik dondurulabilir.");
            if (request.Status == MembershipStatus.Active && membership.Status != MembershipStatus.Frozen) throw new InvalidOperationException("Yalnızca donmuş üyelik yeniden aktifleştirilebilir.");
            if (request.Status == MembershipStatus.Active && (request.EndDate ?? membership.EndDate) < today) throw new InvalidOperationException("Süresi geçmiş üyelik yeniden aktifleştirilemez. Önce bitiş tarihini uzatın.");
            if (request.Status == MembershipStatus.Active)
            {
                var endDate = request.EndDate ?? membership.EndDate;
                var overlaps = await db.StudentMemberships.AnyAsync(x => x.Id != membership.Id && x.StudentId == membership.StudentId && x.MembershipPlanId == membership.MembershipPlanId && (x.Status == MembershipStatus.Active || x.Status == MembershipStatus.Frozen) && (x.EndDate == null || x.EndDate >= membership.StartDate) && (endDate == null || x.StartDate <= endDate), ct);
                if (overlaps) throw new InvalidOperationException("Öğrencinin aynı plan için çakışan aktif veya donmuş bir üyeliği bulunuyor.");
            }
            if (request.Status is MembershipStatus.Cancelled or MembershipStatus.Expired && membership.Status is MembershipStatus.Cancelled or MembershipStatus.Expired) throw new InvalidOperationException("Üyelik zaten sonlandırılmış.");
            membership.Status = request.Status;
            if (request.EndDate.HasValue) { if (request.EndDate < membership.StartDate) throw new InvalidOperationException("Bitiş tarihi başlangıç tarihinden önce olamaz."); membership.EndDate = request.EndDate; }
            if (request.Status is MembershipStatus.Cancelled or MembershipStatus.Expired && membership.EndDate is null) membership.EndDate = today;
            await db.SaveChangesAsync(ct);
            await auditWriter.WriteAsync("MembershipStatusChanged", nameof(StudentMembership), id.ToString(), old, new { membership.Status, membership.EndDate, ChangedBy = userId }, ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return (await GetMembershipsAsync(null, null, ct)).Single(x => x.Id == id);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            throw new InvalidOperationException("Üyelik bilgisi eşzamanlı olarak değişti. Güncel üyelikleri kontrol edip tekrar deneyin.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure })
        {
            throw new InvalidOperationException("Üyelik bilgisi eşzamanlı olarak değişti. Güncel üyelikleri kontrol edip tekrar deneyin.");
        }
    }

    public async Task<IReadOnlyCollection<MembershipPlanResponse>> GetPlansAsync(bool activeOnly, CancellationToken ct) => await db.MembershipPlans.AsNoTracking().Where(x => !activeOnly || x.IsActive).OrderBy(x => x.Name).Select(x => new MembershipPlanResponse(x.Id, x.Name, x.Type, x.DefaultPrice, x.LessonCount, x.DurationMonths, x.IsActive)).ToListAsync(ct);
    public async Task<MembershipPlanResponse> CreatePlanAsync(MembershipPlanRequest r, CancellationToken ct) { ValidatePlan(r); var x = new MembershipPlan { Name = r.Name.Trim(), Type = r.Type, DefaultPrice = r.DefaultPrice, LessonCount = r.LessonCount, DurationMonths = r.DurationMonths, IsActive = r.IsActive }; db.Add(x); await db.SaveChangesAsync(ct); await auditWriter.WriteAsync("MembershipPlanCreated", nameof(MembershipPlan), x.Id.ToString(), null, r, ct); return Map(x); }
    public async Task<MembershipPlanResponse?> UpdatePlanAsync(Guid id, MembershipPlanRequest r, CancellationToken ct) { ValidatePlan(r); var x = await db.MembershipPlans.SingleOrDefaultAsync(x => x.Id == id, ct); if (x is null) return null; var old = Map(x); x.Name = r.Name.Trim(); x.Type = r.Type; x.DefaultPrice = r.DefaultPrice; x.LessonCount = r.LessonCount; x.DurationMonths = r.DurationMonths; x.IsActive = r.IsActive; await db.SaveChangesAsync(ct); await auditWriter.WriteAsync("MembershipPlanUpdated", nameof(MembershipPlan), id.ToString(), old, r, ct); return Map(x); }

    public async Task<IReadOnlyCollection<PaymentListItemResponse>> GetPaymentsAsync(DateOnly? from, DateOnly? to, string? search, CancellationToken ct)
    { var q = db.Payments.AsNoTracking().Where(x => !x.Student.IsDeleted); if (from.HasValue) q = q.Where(x => x.PaymentDate >= IstanbulStart(from.Value)); if (to.HasValue) q = q.Where(x => x.PaymentDate < IstanbulStart(to.Value).AddDays(1)); if (!string.IsNullOrWhiteSpace(search)) { var s = search.Trim().ToLower(); q = q.Where(x => (x.Student.FirstName + " " + x.Student.LastName).ToLower().Contains(s)); } return await q.OrderByDescending(x => x.PaymentDate).Select(x => new PaymentListItemResponse(x.Id, x.StudentId, x.Student.FirstName + " " + x.Student.LastName, x.Amount, x.PaymentDate, x.PaymentMethod, x.InvoiceId, x.Invoice == null ? null : x.Invoice.Description, x.Notes)).ToListAsync(ct); }
    public async Task<IReadOnlyCollection<InvoiceOptionResponse>> GetOpenInvoicesAsync(Guid studentId, CancellationToken ct)
    {
        var rows = await db.Invoices.AsNoTracking()
            .Where(x => x.StudentId == studentId && x.Status != InvoiceStatus.Paid && x.Status != InvoiceStatus.Cancelled)
            .OrderBy(x => x.DueDate)
            .Select(x => new
            {
                x.Id,
                x.Description,
                x.Amount,
                Paid = db.Payments.Where(payment => payment.InvoiceId == x.Id).Sum(payment => (decimal?)payment.Amount) ?? 0,
                x.DueDate,
                x.Status,
            }).ToListAsync(ct);
        return rows
            .Select(x => new InvoiceOptionResponse(x.Id, x.Description, x.Amount, x.Paid, InvoiceBalanceRules.Remaining(x.Status, x.Amount, x.Paid), x.DueDate, x.Status))
            .Where(x => x.Remaining > 0)
            .ToList();
    }
    public async Task<PaymentListItemResponse> CreatePaymentAsync(CreatePaymentRequest r, string userId, CancellationToken ct)
    {
        try
        {
            await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
            if (r.Amount <= 0) throw new InvalidOperationException("Ödeme tutarı sıfırdan büyük olmalıdır.");
            if (!await db.Students.AnyAsync(x => x.Id == r.StudentId && !x.IsDeleted, ct)) throw new InvalidOperationException("Öğrenci bulunamadı.");
            Invoice? invoice = null;
            if (r.InvoiceId.HasValue)
            {
                invoice = await db.Invoices.SingleOrDefaultAsync(x => x.Id == r.InvoiceId && x.StudentId == r.StudentId && x.Status != InvoiceStatus.Paid && x.Status != InvoiceStatus.Cancelled, ct) ?? throw new InvalidOperationException("Açık borç bulunamadı.");
                var paid = await db.Payments.Where(x => x.InvoiceId == invoice.Id).SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
                if (paid + r.Amount > invoice.Amount) throw new InvalidOperationException("Ödeme açık borç tutarını aşamaz.");
                invoice.Status = paid + r.Amount == invoice.Amount ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
            }
            var payment = new Payment { StudentId = r.StudentId, InvoiceId = r.InvoiceId, Amount = r.Amount, PaymentMethod = r.PaymentMethod, PaymentDate = r.PaymentDate?.ToUniversalTime() ?? DateTime.UtcNow, Notes = Clean(r.Notes), ReceivedByUserId = userId };
            db.Add(payment); await db.SaveChangesAsync(ct);
            await auditWriter.WriteAsync("PaymentCreated", nameof(Payment), payment.Id.ToString(), null, new { payment.StudentId, payment.InvoiceId, payment.Amount, payment.PaymentMethod, payment.PaymentDate }, ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return (await GetPaymentsAsync(null, null, null, ct)).Single(x => x.Id == payment.Id);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            throw new InvalidOperationException("Aynı borç için eşzamanlı bir ödeme kaydedildi. Güncel bakiyeyi kontrol edip tekrar deneyin.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure })
        {
            throw new InvalidOperationException("Aynı borç için eşzamanlı bir ödeme kaydedildi. Güncel bakiyeyi kontrol edip tekrar deneyin.");
        }
    }

    public async Task<BalancesResponse> GetBalancesAsync(string? search, bool overdueOnly, bool openOnly, bool includeSettled, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
        var invoiceRows = await db.Invoices.AsNoTracking()
            .Where(invoice => !invoice.Student.IsDeleted && invoice.Status != InvoiceStatus.Cancelled)
            .Select(invoice => new
            {
                invoice.StudentId,
                invoice.Amount,
                invoice.DueDate,
                invoice.Status,
                Paid = db.Payments.Where(payment => payment.InvoiceId == invoice.Id).Sum(payment => (decimal?)payment.Amount) ?? 0m,
            }).ToListAsync(ct);

        var aggregates = invoiceRows
            .GroupBy(x => x.StudentId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var rows = group.Select(x => new
                    {
                        x.Amount,
                        x.Paid,
                        x.DueDate,
                        Remaining = InvoiceBalanceRules.Remaining(x.Status, x.Amount, x.Paid),
                    }).ToList();
                    return new BalanceAggregate(
                        rows.Sum(x => x.Amount),
                        rows.Sum(x => x.Paid),
                        rows.Sum(x => x.Remaining),
                        rows.Where(x => x.DueDate < today).Sum(x => x.Remaining),
                        rows.Count(x => x.Remaining > 0),
                        rows.Count(x => x.DueDate < today && x.Remaining > 0));
                });

        var students = db.Students.AsNoTracking().Where(x => !x.IsDeleted);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            students = students.Where(x => (x.FirstName + " " + x.LastName).ToLower().Contains(normalizedSearch));
        }
        var studentRows = await students
            .OrderBy(x => x.FirstName).ThenBy(x => x.LastName)
            .Select(x => new
            {
                x.Id,
                Name = x.FirstName + " " + x.LastName,
                LastPaymentDate = db.Payments.Where(payment => payment.StudentId == x.Id).Max(payment => (DateTime?)payment.PaymentDate),
            }).ToListAsync(ct);

        var items = studentRows.Select(student =>
        {
            var aggregate = aggregates.GetValueOrDefault(student.Id) ?? BalanceAggregate.Empty;
            var status = aggregate.OverdueBalance > 0 ? DebtStatus.Overdue : aggregate.Remaining > 0 ? DebtStatus.Open : DebtStatus.None;
            return new BalanceListItemResponse(student.Id, student.Name, aggregate.TotalDebt, aggregate.Paid, aggregate.Remaining, student.LastPaymentDate, aggregate.OverdueBalance, aggregate.OpenInvoiceCount, aggregate.OverdueInvoiceCount, status);
        });
        if (!includeSettled || openOnly) items = items.Where(x => x.Remaining > 0);
        if (overdueOnly) items = items.Where(x => x.OverdueBalance > 0);
        var result = items.OrderByDescending(x => x.OverdueBalance).ThenByDescending(x => x.Remaining).ThenBy(x => x.StudentName).ToList();

        var monthStart = IstanbulStart(new DateOnly(today.Year, today.Month, 1));
        var monthEnd = IstanbulStart(new DateOnly(today.Year, today.Month, 1).AddMonths(1));
        var collected = await db.Payments.AsNoTracking().Where(x => !x.Student.IsDeleted && x.PaymentDate >= monthStart && x.PaymentDate < monthEnd).SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
        var summary = new BalanceSummaryResponse(aggregates.Values.Sum(x => x.Remaining), aggregates.Values.Count(x => x.Remaining > 0), collected, aggregates.Values.Sum(x => x.OverdueBalance));
        return new(summary, result);
    }

    public async Task<StudentFinanceOverviewResponse?> GetStudentFinanceOverviewAsync(Guid studentId, CancellationToken ct)
    {
        if (!await db.Students.AsNoTracking().AnyAsync(x => x.Id == studentId && !x.IsDeleted, ct)) return null;
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
        var memberships = await db.StudentMemberships.AsNoTracking().Where(x => x.StudentId == studentId)
            .OrderByDescending(x => x.Status == MembershipStatus.Active).ThenByDescending(x => x.StartDate).Take(5)
            .Select(x => new StudentMembershipOverviewResponse(
                x.Id, x.MembershipPlanId, x.MembershipPlan.Name, x.StartDate, x.EndDate, x.Status == MembershipStatus.Active && x.EndDate < today ? MembershipStatus.Expired : x.Status,
                db.MembershipPriceHistory.Where(p => p.StudentMembershipId == x.Id && p.EffectiveFrom <= today && (p.EffectiveTo == null || p.EffectiveTo >= today)).OrderByDescending(p => p.EffectiveFrom).Select(p => (decimal?)p.Price).FirstOrDefault() ?? x.MembershipPlan.DefaultPrice,
                db.MembershipPriceHistory.Where(p => p.StudentMembershipId == x.Id && p.EffectiveFrom <= today && (p.EffectiveTo == null || p.EffectiveTo >= today)).OrderByDescending(p => p.EffectiveFrom).Select(p => p.DiscountAmount).FirstOrDefault(),
                db.MembershipPriceHistory.Where(p => p.StudentMembershipId == x.Id && p.EffectiveFrom <= today && (p.EffectiveTo == null || p.EffectiveTo >= today)).OrderByDescending(p => p.EffectiveFrom).Select(p => p.DiscountReason).FirstOrDefault()))
            .ToListAsync(ct);
        var invoiceRows = await db.Invoices.AsNoTracking().Where(x => x.StudentId == studentId && x.Status != InvoiceStatus.Cancelled)
            .Select(x => new { Invoice = x, Paid = db.Payments.Where(p => p.InvoiceId == x.Id).Sum(p => (decimal?)p.Amount) ?? 0 })
            .ToListAsync(ct);
        var invoices = invoiceRows.OrderByDescending(x => x.Invoice.DueDate).Take(5)
            .Select(x => new StudentInvoiceHistoryResponse(x.Invoice.Id, x.Invoice.Description, x.Invoice.Amount, x.Paid, InvoiceBalanceRules.Remaining(x.Invoice.Status, x.Invoice.Amount, x.Paid), x.Invoice.DueDate, x.Invoice.Status)).ToList();
        var payments = await db.Payments.AsNoTracking().Where(x => x.StudentId == studentId).OrderByDescending(x => x.PaymentDate).Take(5)
            .Select(x => new StudentPaymentHistoryResponse(x.Id, x.InvoiceId, x.Invoice == null ? null : x.Invoice.Description, x.Amount, x.PaymentDate, x.PaymentMethod, x.Notes)).ToListAsync(ct);
        var totalInvoiced = invoiceRows.Sum(x => x.Invoice.Amount); var totalPaid = invoiceRows.Sum(x => x.Paid);
        var openBalance = invoiceRows.Sum(x => InvoiceBalanceRules.Remaining(x.Invoice.Status, x.Invoice.Amount, x.Paid));
        var overdue = invoiceRows.Where(x => x.Invoice.DueDate < today).Sum(x => InvoiceBalanceRules.Remaining(x.Invoice.Status, x.Invoice.Amount, x.Paid));
        return new(totalInvoiced, totalPaid, openBalance, overdue, memberships, invoices, payments);
    }

    public async Task<ReportsResponse> GetReportsAsync(CancellationToken ct) { var balances = await GetBalancesAsync(null, false, false, false, ct); var activeStudents = await db.Students.CountAsync(x => !x.IsDeleted && x.Status == StudentStatus.Active, ct); var activeClasses = await db.StudioClasses.CountAsync(x => !x.IsDeleted && x.Status == StudioClassStatus.Active, ct); var occupancy = activeClasses == 0 ? 0 : await db.StudioClasses.Where(x => !x.IsDeleted && x.Status == StudioClassStatus.Active).AverageAsync(x => 100m * db.ClassEnrollments.Count(e => e.StudioClassId == x.Id && e.Status == EnrollmentStatus.Active) / x.Capacity, ct); var attendanceTotal = await db.Attendances.CountAsync(ct); var attendanceRate = attendanceTotal == 0 ? 0 : 100m * await db.Attendances.CountAsync(x => x.Status == AttendanceStatus.Present || x.Status == AttendanceStatus.Late, ct) / attendanceTotal; var now = DateTime.UtcNow.AddHours(3); var monthly = new List<ReportPointResponse>(); for (var i = 5; i >= 0; i--) { var local = new DateOnly(now.Year, now.Month, 1).AddMonths(-i); var start = IstanbulStart(local); var end = IstanbulStart(local.AddMonths(1)); monthly.Add(new(local.ToString("yyyy-MM"), await db.Payments.Where(x => x.PaymentDate >= start && x.PaymentDate < end).SumAsync(x => (decimal?)x.Amount, ct) ?? 0)); } var statuses = await db.Students.Where(x => !x.IsDeleted).GroupBy(x => x.Status).Select(x => new ReportPointResponse(x.Key.ToString(), x.Count())).ToListAsync(ct); var classes = await db.StudioClasses.Where(x => !x.IsDeleted && x.Status == StudioClassStatus.Active).OrderBy(x => x.Name).Select(x => new ReportPointResponse(x.Name, 100m * db.ClassEnrollments.Count(e => e.StudioClassId == x.Id && e.Status == EnrollmentStatus.Active) / x.Capacity)).ToListAsync(ct); return new(activeStudents, activeClasses, balances.Summary.CollectedThisMonth, balances.Summary.OpenBalance, Math.Round(occupancy, 1), Math.Round(attendanceRate, 1), monthly, statuses, classes); }
    public async Task<InstructorDetailResponse?> GetInstructorAsync(Guid id, CancellationToken ct) { var x = await db.Instructors.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct); if (x is null) return null; var linked = x.UserId == null ? null : await db.Users.Where(u => u.Id == x.UserId).Select(u => u.DisplayName).SingleOrDefaultAsync(ct); var schedule = await GetScheduleAsync(null, id, null, null, ct); return new(x.Id, x.FirstName, x.LastName, x.Phone, x.Email, x.UserId, linked, x.IsDeleted, await db.StudioClasses.CountAsync(c => c.InstructorId == id && !c.IsDeleted && c.Status == StudioClassStatus.Active, ct), schedule); }
    public async Task<GlobalSearchResponse> SearchAsync(string query, string? userId, bool instructorOnly, CancellationToken ct)
    {
        var cleaned = query.Trim(); if (cleaned.Length < 2) return new([]);
        var lowered = cleaned.ToLower();
        var students = db.Students.AsNoTracking().Where(x => !x.IsDeleted && (x.FirstName.ToLower().Contains(lowered) || x.LastName.ToLower().Contains(lowered) || (x.FirstName + " " + x.LastName).ToLower().Contains(lowered) || (x.Phone != null && x.Phone.Contains(cleaned)) || (x.Email != null && x.Email.ToLower().Contains(lowered))));
        if (instructorOnly) students = students.Where(x => db.ClassEnrollments.Any(e => e.StudentId == x.Id && e.Status == EnrollmentStatus.Active && !e.StudioClass.IsDeleted && e.StudioClass.Instructor.UserId == userId));
        var studentRows = await students.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).Take(6).Select(x => new { x.Id, Label = x.FirstName + " " + x.LastName, Detail = x.Phone ?? x.Email }).ToListAsync(ct);
        var studentItems = studentRows.Select(x => new GlobalSearchItemResponse("Student", x.Id.ToString(), x.Label, x.Detail, instructorOnly ? $"/attendance?studentId={x.Id}" : $"/students/detail?id={x.Id}")).ToList();

        var classes = db.StudioClasses.AsNoTracking().Where(x => !x.IsDeleted && (x.Name.ToLower().Contains(lowered) || x.StudioRoom.Name.ToLower().Contains(lowered) || (x.Instructor.FirstName + " " + x.Instructor.LastName).ToLower().Contains(lowered)));
        if (instructorOnly) classes = classes.Where(x => x.Instructor.UserId == userId);
        var classItems = await classes.OrderBy(x => x.Name).Take(6).Select(x => new GlobalSearchItemResponse("Class", x.Id.ToString(), x.Name, x.Instructor.FirstName + " " + x.Instructor.LastName + " · " + x.StudioRoom.Name, $"/classes/detail?id={x.Id}")).ToListAsync(ct);

        var items = new List<GlobalSearchItemResponse>(studentItems); items.AddRange(classItems);
        if (!instructorOnly)
        {
            var instructors = await db.Instructors.AsNoTracking().Where(x => !x.IsDeleted && ((x.FirstName + " " + x.LastName).ToLower().Contains(lowered) || (x.Phone != null && x.Phone.Contains(cleaned)) || (x.Email != null && x.Email.ToLower().Contains(lowered))))
                .OrderBy(x => x.LastName).ThenBy(x => x.FirstName).Take(6).Select(x => new GlobalSearchItemResponse("Instructor", x.Id.ToString(), x.FirstName + " " + x.LastName, x.Phone ?? x.Email, "/instructors")).ToListAsync(ct);
            items.AddRange(instructors);
        }
        return new(items.Take(12).ToList());
    }
    private static MembershipPlanResponse Map(MembershipPlan x) => new(x.Id, x.Name, x.Type, x.DefaultPrice, x.LessonCount, x.DurationMonths, x.IsActive);
    private sealed record BalanceAggregate(decimal TotalDebt, decimal Paid, decimal Remaining, decimal OverdueBalance, int OpenInvoiceCount, int OverdueInvoiceCount)
    {
        public static readonly BalanceAggregate Empty = new(0, 0, 0, 0, 0, 0);
    }
    private static void ValidatePlan(MembershipPlanRequest r) { if (string.IsNullOrWhiteSpace(r.Name) || r.DefaultPrice < 0 || r.LessonCount < 0 || r.DurationMonths < 0) throw new InvalidOperationException("Üyelik planı alanları geçersiz."); }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
