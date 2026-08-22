using Border.Application.Operations;
using Border.Domain.Entities;
using Border.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Border.Infrastructure.Operations;

internal sealed class OperationsService(BorderDbContext db) : IOperationsService
{
    private static DateTime IstanbulStart(DateOnly date) => DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified).AddHours(-3);

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

    public async Task<IReadOnlyCollection<SessionListItemResponse>> GetSessionsAsync(DateOnly date, string? userId, bool instructorOnly, CancellationToken ct)
    {
        var start = IstanbulStart(date); var end = start.AddDays(1);
        var query = db.LessonSessions.AsNoTracking().Where(x => x.ScheduledStart >= start && x.ScheduledStart < end && x.Status != LessonSessionStatus.Cancelled);
        if (instructorOnly) query = query.Where(x => x.Instructor.UserId == userId);
        return await query.OrderBy(x => x.ScheduledStart).Select(x => new SessionListItemResponse(x.Id, x.StudioClassId, x.StudioClass.Name, x.Instructor.FirstName + " " + x.Instructor.LastName, x.StudioRoom.Name, x.ScheduledStart, x.ScheduledEnd,
            db.ClassEnrollments.Count(e => e.StudioClassId == x.StudioClassId && e.Status == EnrollmentStatus.Active && e.StartDate <= date && (e.EndDate == null || e.EndDate >= date)),
            x.Status == LessonSessionStatus.Completed || db.Attendances.Any(a => a.LessonSessionId == x.Id))).ToListAsync(ct);
    }

    public async Task<AttendanceDetailResponse?> GetAttendanceAsync(Guid sessionId, string? userId, bool instructorOnly, CancellationToken ct)
    {
        var session = await db.LessonSessions.AsNoTracking().Where(x => x.Id == sessionId && x.Status != LessonSessionStatus.Cancelled && (!instructorOnly || x.Instructor.UserId == userId))
            .Select(x => new SessionListItemResponse(x.Id, x.StudioClassId, x.StudioClass.Name, x.Instructor.FirstName + " " + x.Instructor.LastName, x.StudioRoom.Name, x.ScheduledStart, x.ScheduledEnd, 0, x.Status == LessonSessionStatus.Completed || db.Attendances.Any(a => a.LessonSessionId == x.Id))).SingleOrDefaultAsync(ct);
        if (session is null) return null;
        var date = DateOnly.FromDateTime(session.ScheduledStart.AddHours(3));
        var students = await db.ClassEnrollments.AsNoTracking().Where(x => x.StudioClassId == session.ClassId && x.Status == EnrollmentStatus.Active && x.StartDate <= date && (x.EndDate == null || x.EndDate >= date) && !x.Student.IsDeleted)
            .OrderBy(x => x.Student.FirstName).ThenBy(x => x.Student.LastName)
            .Select(x => new AttendanceStudentResponse(x.StudentId, x.Student.FirstName + " " + x.Student.LastName, db.Attendances.Where(a => a.LessonSessionId == sessionId && a.StudentId == x.StudentId).Select(a => (AttendanceStatus?)a.Status).FirstOrDefault(), db.Attendances.Where(a => a.LessonSessionId == sessionId && a.StudentId == x.StudentId).Select(a => a.Notes).FirstOrDefault())).ToListAsync(ct);
        return new(session with { StudentCount = students.Count }, students);
    }

    public async Task<AttendanceDetailResponse?> SaveAttendanceAsync(Guid sessionId, SaveAttendanceRequest request, string userId, bool instructorOnly, CancellationToken ct)
    {
        var detail = await GetAttendanceAsync(sessionId, userId, instructorOnly, ct); if (detail is null) return null;
        var allowed = detail.Students.Select(x => x.StudentId).ToHashSet();
        if (request.Entries.Count != request.Entries.Select(x => x.StudentId).Distinct().Count() || request.Entries.Any(x => !allowed.Contains(x.StudentId))) throw new InvalidOperationException("Yoklama listesinde sınıfa kayıtlı olmayan veya yinelenen öğrenci var.");
        var existing = await db.Attendances.Where(x => x.LessonSessionId == sessionId).ToDictionaryAsync(x => x.StudentId, ct);
        foreach (var item in request.Entries) { if (existing.TryGetValue(item.StudentId, out var row)) { row.Status = item.Status; row.Notes = Clean(item.Notes); row.UpdatedAt = DateTime.UtcNow; row.RecordedByUserId = userId; } else db.Attendances.Add(new Attendance { LessonSessionId = sessionId, StudentId = item.StudentId, Status = item.Status, Notes = Clean(item.Notes), RecordedByUserId = userId }); }
        var session = await db.LessonSessions.SingleAsync(x => x.Id == sessionId, ct); session.Status = LessonSessionStatus.Completed;
        await db.SaveChangesAsync(ct); return await GetAttendanceAsync(sessionId, userId, instructorOnly, ct);
    }

    public async Task<IReadOnlyCollection<MembershipListItemResponse>> GetMembershipsAsync(string? search, MembershipStatus? status, CancellationToken ct)
    {
        var q = db.StudentMemberships.AsNoTracking().Where(x => !x.Student.IsDeleted); if (status.HasValue) q = q.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(search)) { var s = search.Trim().ToLower(); q = q.Where(x => (x.Student.FirstName + " " + x.Student.LastName).ToLower().Contains(s)); }
        return await q.OrderByDescending(x => x.StartDate).Select(x => new MembershipListItemResponse(x.Id, x.StudentId, x.Student.FirstName + " " + x.Student.LastName, x.MembershipPlanId, x.MembershipPlan.Name, x.MembershipPlan.Type, x.StartDate, x.EndDate, x.Status,
            db.MembershipPriceHistory.Where(p => p.StudentMembershipId == x.Id).OrderByDescending(p => p.EffectiveFrom).Select(p => p.Price - (p.DiscountAmount ?? 0)).FirstOrDefault(), x.MembershipPlan.LessonCount)).ToListAsync(ct);
    }

    public async Task<MembershipListItemResponse> CreateMembershipAsync(CreateMembershipRequest request, string userId, CancellationToken ct)
    {
        var plan = await db.MembershipPlans.SingleOrDefaultAsync(x => x.Id == request.PlanId && x.IsActive, ct) ?? throw new InvalidOperationException("Üyelik planı bulunamadı veya pasif.");
        if (!await db.Students.AnyAsync(x => x.Id == request.StudentId && !x.IsDeleted, ct)) throw new InvalidOperationException("Öğrenci bulunamadı.");
        if (request.StartDate == default || request.EndDate < request.StartDate) throw new InvalidOperationException("Üyelik tarihleri geçersiz.");
        var price = request.Price ?? plan.DefaultPrice; var discount = request.DiscountAmount ?? 0; if (price < 0 || discount < 0 || discount > price) throw new InvalidOperationException("Üyelik ücreti veya indirim geçersiz.");
        var membership = new StudentMembership { StudentId = request.StudentId, MembershipPlanId = request.PlanId, StartDate = request.StartDate, EndDate = request.EndDate ?? (plan.DurationMonths.HasValue ? request.StartDate.AddMonths(plan.DurationMonths.Value) : null), Status = MembershipStatus.Active };
        db.StudentMemberships.Add(membership); db.MembershipPriceHistory.Add(new MembershipPriceHistory { StudentMembership = membership, Price = price, DiscountAmount = discount, DiscountReason = Clean(request.DiscountReason), EffectiveFrom = request.StartDate, ApprovedByUserId = userId });
        if (price - discount > 0) db.Invoices.Add(new Invoice { StudentId = request.StudentId, StudentMembership = membership, Description = plan.Name + " üyeliği", Amount = price - discount, DueDate = request.StartDate, Status = InvoiceStatus.Pending });
        await db.SaveChangesAsync(ct); return (await GetMembershipsAsync(null, null, ct)).Single(x => x.Id == membership.Id);
    }

    public async Task<IReadOnlyCollection<MembershipPlanResponse>> GetPlansAsync(bool activeOnly, CancellationToken ct) => await db.MembershipPlans.AsNoTracking().Where(x => !activeOnly || x.IsActive).OrderBy(x => x.Name).Select(x => new MembershipPlanResponse(x.Id, x.Name, x.Type, x.DefaultPrice, x.LessonCount, x.DurationMonths, x.IsActive)).ToListAsync(ct);
    public async Task<MembershipPlanResponse> CreatePlanAsync(MembershipPlanRequest r, CancellationToken ct) { ValidatePlan(r); var x = new MembershipPlan { Name = r.Name.Trim(), Type = r.Type, DefaultPrice = r.DefaultPrice, LessonCount = r.LessonCount, DurationMonths = r.DurationMonths, IsActive = r.IsActive }; db.Add(x); await db.SaveChangesAsync(ct); return Map(x); }
    public async Task<MembershipPlanResponse?> UpdatePlanAsync(Guid id, MembershipPlanRequest r, CancellationToken ct) { ValidatePlan(r); var x = await db.MembershipPlans.SingleOrDefaultAsync(x => x.Id == id, ct); if (x is null) return null; x.Name = r.Name.Trim(); x.Type = r.Type; x.DefaultPrice = r.DefaultPrice; x.LessonCount = r.LessonCount; x.DurationMonths = r.DurationMonths; x.IsActive = r.IsActive; await db.SaveChangesAsync(ct); return Map(x); }

    public async Task<IReadOnlyCollection<PaymentListItemResponse>> GetPaymentsAsync(DateOnly? from, DateOnly? to, string? search, CancellationToken ct)
    { var q = db.Payments.AsNoTracking().Where(x => !x.Student.IsDeleted); if (from.HasValue) q = q.Where(x => x.PaymentDate >= IstanbulStart(from.Value)); if (to.HasValue) q = q.Where(x => x.PaymentDate < IstanbulStart(to.Value).AddDays(1)); if (!string.IsNullOrWhiteSpace(search)) { var s = search.Trim().ToLower(); q = q.Where(x => (x.Student.FirstName + " " + x.Student.LastName).ToLower().Contains(s)); } return await q.OrderByDescending(x => x.PaymentDate).Select(x => new PaymentListItemResponse(x.Id, x.StudentId, x.Student.FirstName + " " + x.Student.LastName, x.Amount, x.PaymentDate, x.PaymentMethod, x.InvoiceId, x.Invoice == null ? null : x.Invoice.Description, x.Notes)).ToListAsync(ct); }
    public async Task<IReadOnlyCollection<InvoiceOptionResponse>> GetOpenInvoicesAsync(Guid studentId, CancellationToken ct) => await db.Invoices.AsNoTracking().Where(x => x.StudentId == studentId && x.Status != InvoiceStatus.Paid && x.Status != InvoiceStatus.Cancelled).OrderBy(x => x.DueDate).Select(x => new InvoiceOptionResponse(x.Id, x.Description, x.Amount, db.Payments.Where(p => p.InvoiceId == x.Id).Sum(p => (decimal?)p.Amount) ?? 0, x.Amount - (db.Payments.Where(p => p.InvoiceId == x.Id).Sum(p => (decimal?)p.Amount) ?? 0), x.DueDate)).Where(x => x.Remaining > 0).ToListAsync(ct);
    public async Task<PaymentListItemResponse> CreatePaymentAsync(CreatePaymentRequest r, string userId, CancellationToken ct) { if (r.Amount <= 0) throw new InvalidOperationException("Ödeme tutarı sıfırdan büyük olmalıdır."); if (!await db.Students.AnyAsync(x => x.Id == r.StudentId && !x.IsDeleted, ct)) throw new InvalidOperationException("Öğrenci bulunamadı."); Invoice? invoice = null; if (r.InvoiceId.HasValue) { invoice = await db.Invoices.SingleOrDefaultAsync(x => x.Id == r.InvoiceId && x.StudentId == r.StudentId && x.Status != InvoiceStatus.Cancelled, ct) ?? throw new InvalidOperationException("Açık borç bulunamadı."); var paid = await db.Payments.Where(x => x.InvoiceId == invoice.Id).SumAsync(x => (decimal?)x.Amount, ct) ?? 0; if (paid + r.Amount > invoice.Amount) throw new InvalidOperationException("Ödeme açık borç tutarını aşamaz."); invoice.Status = paid + r.Amount == invoice.Amount ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid; } var p = new Payment { StudentId = r.StudentId, InvoiceId = r.InvoiceId, Amount = r.Amount, PaymentMethod = r.PaymentMethod, PaymentDate = r.PaymentDate?.ToUniversalTime() ?? DateTime.UtcNow, Notes = Clean(r.Notes), ReceivedByUserId = userId }; db.Add(p); await db.SaveChangesAsync(ct); return (await GetPaymentsAsync(null, null, null, ct)).Single(x => x.Id == p.Id); }

    public async Task<BalancesResponse> GetBalancesAsync(string? search, CancellationToken ct) { var students = db.Students.AsNoTracking().Where(x => !x.IsDeleted); if (!string.IsNullOrWhiteSpace(search)) { var s = search.Trim().ToLower(); students = students.Where(x => (x.FirstName + " " + x.LastName).ToLower().Contains(s)); } var items = await students.Select(x => new BalanceListItemResponse(x.Id, x.FirstName + " " + x.LastName, db.Invoices.Where(i => i.StudentId == x.Id && i.Status != InvoiceStatus.Cancelled).Sum(i => (decimal?)i.Amount) ?? 0, db.Payments.Where(p => p.StudentId == x.Id && p.InvoiceId != null).Sum(p => (decimal?)p.Amount) ?? 0, (db.Invoices.Where(i => i.StudentId == x.Id && i.Status != InvoiceStatus.Cancelled).Sum(i => (decimal?)i.Amount) ?? 0) - (db.Payments.Where(p => p.StudentId == x.Id && p.InvoiceId != null).Sum(p => (decimal?)p.Amount) ?? 0), db.Payments.Where(p => p.StudentId == x.Id).Max(p => (DateTime?)p.PaymentDate))).Where(x => x.Remaining > 0).OrderByDescending(x => x.Remaining).ToListAsync(ct); var month = new DateOnly(DateTime.UtcNow.AddHours(3).Year, DateTime.UtcNow.AddHours(3).Month, 1); var monthStart = IstanbulStart(month); var collected = await db.Payments.Where(x => x.PaymentDate >= monthStart).SumAsync(x => (decimal?)x.Amount, ct) ?? 0; var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3)); var overdue = await db.Invoices.Where(x => x.DueDate < today && x.Status != InvoiceStatus.Paid && x.Status != InvoiceStatus.Cancelled).SumAsync(x => (decimal?)x.Amount, ct) ?? 0; var overduePaid = await db.Payments.Where(x => x.InvoiceId != null && x.Invoice!.DueDate < today && x.Invoice.Status != InvoiceStatus.Cancelled).SumAsync(x => (decimal?)x.Amount, ct) ?? 0; return new(new(items.Sum(x => x.Remaining), items.Count, collected, Math.Max(0, overdue - overduePaid)), items); }

    public async Task<ReportsResponse> GetReportsAsync(CancellationToken ct) { var balances = await GetBalancesAsync(null, ct); var activeStudents = await db.Students.CountAsync(x => !x.IsDeleted && x.Status == StudentStatus.Active, ct); var activeClasses = await db.StudioClasses.CountAsync(x => !x.IsDeleted && x.Status == StudioClassStatus.Active, ct); var occupancy = activeClasses == 0 ? 0 : await db.StudioClasses.Where(x => !x.IsDeleted && x.Status == StudioClassStatus.Active).AverageAsync(x => 100m * db.ClassEnrollments.Count(e => e.StudioClassId == x.Id && e.Status == EnrollmentStatus.Active) / x.Capacity, ct); var attendanceTotal = await db.Attendances.CountAsync(ct); var attendanceRate = attendanceTotal == 0 ? 0 : 100m * await db.Attendances.CountAsync(x => x.Status == AttendanceStatus.Present || x.Status == AttendanceStatus.Late, ct) / attendanceTotal; var now = DateTime.UtcNow.AddHours(3); var monthly = new List<ReportPointResponse>(); for (var i = 5; i >= 0; i--) { var local = new DateOnly(now.Year, now.Month, 1).AddMonths(-i); var start = IstanbulStart(local); var end = IstanbulStart(local.AddMonths(1)); monthly.Add(new(local.ToString("yyyy-MM"), await db.Payments.Where(x => x.PaymentDate >= start && x.PaymentDate < end).SumAsync(x => (decimal?)x.Amount, ct) ?? 0)); } var statuses = await db.Students.Where(x => !x.IsDeleted).GroupBy(x => x.Status).Select(x => new ReportPointResponse(x.Key.ToString(), x.Count())).ToListAsync(ct); var classes = await db.StudioClasses.Where(x => !x.IsDeleted && x.Status == StudioClassStatus.Active).OrderBy(x => x.Name).Select(x => new ReportPointResponse(x.Name, 100m * db.ClassEnrollments.Count(e => e.StudioClassId == x.Id && e.Status == EnrollmentStatus.Active) / x.Capacity)).ToListAsync(ct); return new(activeStudents, activeClasses, balances.Summary.CollectedThisMonth, balances.Summary.OpenBalance, Math.Round(occupancy, 1), Math.Round(attendanceRate, 1), monthly, statuses, classes); }
    public async Task<InstructorDetailResponse?> GetInstructorAsync(Guid id, CancellationToken ct) { var x = await db.Instructors.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct); if (x is null) return null; var linked = x.UserId == null ? null : await db.Users.Where(u => u.Id == x.UserId).Select(u => u.DisplayName).SingleOrDefaultAsync(ct); var schedule = await GetScheduleAsync(null, id, null, null, ct); return new(x.Id, x.FirstName, x.LastName, x.Phone, x.Email, x.UserId, linked, x.IsDeleted, await db.StudioClasses.CountAsync(c => c.InstructorId == id && !c.IsDeleted && c.Status == StudioClassStatus.Active, ct), schedule); }
    private static MembershipPlanResponse Map(MembershipPlan x) => new(x.Id, x.Name, x.Type, x.DefaultPrice, x.LessonCount, x.DurationMonths, x.IsActive);
    private static void ValidatePlan(MembershipPlanRequest r) { if (string.IsNullOrWhiteSpace(r.Name) || r.DefaultPrice < 0 || r.LessonCount < 0 || r.DurationMonths < 0) throw new InvalidOperationException("Üyelik planı alanları geçersiz."); }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
