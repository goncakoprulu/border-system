using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Border.Application.Operations;
using Border.Domain.Entities;
using Border.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Border.Tests;

public sealed class DashboardApiTests(StudentApiFactory factory) : IClassFixture<StudentApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    [Fact]
    public async Task Dashboard_ReturnsRealKpisLessonsAlertsAndThirtyDayAnalytics_InIstanbulTime()
    {
        await factory.ResetAsync(); var seed = await SeedOperationalDashboardAsync(); using var client = Client("Reception");
        var operations = await client.GetFromJsonAsync<DashboardOperationsResponse>("/api/dashboard/operations", JsonOptions);
        Assert.Equal(9, operations!.ActiveStudentCount); Assert.Equal(1, operations.TodayLessonCount);
        var lesson = Assert.Single(operations.TodayLessons); Assert.Equal(seed.ClassId, lesson.ClassId); Assert.Equal(9, lesson.StudentCount); Assert.Equal(10, lesson.Capacity);
        Assert.Equal(seed.Today.AddDays(-1), DateOnly.FromDateTime(lesson.ScheduledStart));
        Assert.False(lesson.IsAttendanceCompleted);

        var analytics = await client.GetFromJsonAsync<DashboardAnalyticsResponse>("/api/dashboard/analytics", JsonOptions);
        Assert.True(analytics!.CanViewFinance); Assert.Equal(400m, analytics.MonthlyRevenue); Assert.Equal(600m, analytics.OutstandingBalance);
        Assert.Equal(9, analytics.NewStudents); Assert.Equal(400m, analytics.TotalPayments); Assert.Equal(1, analytics.ActiveMemberships); Assert.Equal(100m, analytics.AttendanceRate);
        Assert.Equal(30, analytics.ThirtyDayRevenue.Count); Assert.Equal(400m, analytics.ThirtyDayRevenue.Sum(x => x.Value));
        Assert.Contains(analytics.Alerts, x => x.Type == "OverdueInvoices" && x.Count == 1);
        Assert.Contains(analytics.Alerts, x => x.Type == "OpenBalances" && x.Count == 1);
        Assert.Contains(analytics.Alerts, x => x.Type == "ExpiringMemberships" && x.Count == 1);
        Assert.Contains(analytics.Alerts, x => x.Type == "MissingAttendance" && x.Count >= 1);
        Assert.Contains(analytics.Alerts, x => x.Type == "NearCapacity" && x.Count == 1);
    }

    [Fact]
    public async Task Dashboard_EmptyData_ReturnsStableEmptyReadModels()
    {
        await factory.ResetAsync(); using var client = Client("Management");
        var operations = await client.GetFromJsonAsync<DashboardOperationsResponse>("/api/dashboard/operations", JsonOptions);
        var analytics = await client.GetFromJsonAsync<DashboardAnalyticsResponse>("/api/dashboard/analytics", JsonOptions);
        Assert.Equal(0, operations!.ActiveStudentCount); Assert.Empty(operations.TodayLessons);
        Assert.True(analytics!.CanViewFinance); Assert.Equal(0m, analytics.MonthlyRevenue); Assert.Equal(0m, analytics.OutstandingBalance);
        Assert.Empty(analytics.Alerts); Assert.Equal(30, analytics.ThirtyDayRevenue.Count); Assert.All(analytics.ThirtyDayRevenue, x => Assert.Equal(0m, x.Value));
    }

    [Fact]
    public async Task Dashboard_InstructorIsScopedAndCannotSeeFinance()
    {
        await factory.ResetAsync(); await SeedOperationalDashboardAsync(); using var client = Client("Instructor", "dashboard-instructor");
        var operations = await client.GetFromJsonAsync<DashboardOperationsResponse>("/api/dashboard/operations", JsonOptions);
        var analytics = await client.GetFromJsonAsync<DashboardAnalyticsResponse>("/api/dashboard/analytics", JsonOptions);
        Assert.Equal(9, operations!.ActiveStudentCount); Assert.Single(operations.TodayLessons);
        Assert.False(analytics!.CanViewFinance); Assert.Equal(0m, analytics.MonthlyRevenue); Assert.Equal(0m, analytics.OutstandingBalance); Assert.Equal(0, analytics.ActiveMemberships);
        Assert.DoesNotContain(analytics.Alerts, x => x.Type is "OverdueInvoices" or "OpenBalances" or "ExpiringMemberships");
        using var member = Client("Member"); Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/dashboard/operations")).StatusCode);
    }

    [Fact]
    public async Task Dashboard_SurfacesEmptyClassLowAttendanceAndUnassignedStudentRisks()
    {
        await factory.ResetAsync(); var seed = await SeedOperationalDashboardAsync();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BorderDbContext>();
            var mainClass = await db.StudioClasses.SingleAsync(x => x.Id == seed.ClassId);
            var student = await db.Students.FirstAsync(x => x.FirstName == "Öğrenci 1");
            db.Students.Add(new Student { FirstName = "Sınıfsız", LastName = "Öğrenci", Status = StudentStatus.Active, RegistrationDate = seed.Today });
            db.StudioClasses.Add(new StudioClass { Name = "Boş Sınıf", InstructorId = mainClass.InstructorId, StudioRoomId = mainClass.StudioRoomId, Capacity = 8, Status = StudioClassStatus.Active, StartDate = seed.Today.AddDays(-1) });
            foreach (var index in Enumerable.Range(3, 3))
            {
                var session = new LessonSession { StudioClassId = mainClass.Id, InstructorId = mainClass.InstructorId, StudioRoomId = mainClass.StudioRoomId, ScheduledStart = IstanbulStart(seed.Today.AddDays(-index)).AddHours(10), ScheduledEnd = IstanbulStart(seed.Today.AddDays(-index)).AddHours(11), Status = LessonSessionStatus.Completed };
                db.LessonSessions.Add(session);
                db.Attendances.Add(new Attendance { LessonSession = session, Student = student, Status = index <= 4 ? AttendanceStatus.Absent : AttendanceStatus.Present, RecordedByUserId = "test-user" });
            }
            await db.SaveChangesAsync();
        }

        using var management = Client("Management");
        var analytics = await management.GetFromJsonAsync<DashboardAnalyticsResponse>("/api/dashboard/analytics", JsonOptions);
        Assert.Contains(analytics!.Alerts, x => x.Type == "EmptyClasses" && x.Count == 1);
        Assert.Contains(analytics.Alerts, x => x.Type == "LowAttendance" && x.Count == 1);
        Assert.Contains(analytics.Alerts, x => x.Type == "UnassignedStudents" && x.Count == 1);

        using var instructor = Client("Instructor", "dashboard-instructor");
        var scoped = await instructor.GetFromJsonAsync<DashboardAnalyticsResponse>("/api/dashboard/analytics", JsonOptions);
        Assert.Contains(scoped!.Alerts, x => x.Type == "EmptyClasses" && x.Count == 1);
        Assert.Contains(scoped.Alerts, x => x.Type == "LowAttendance" && x.Count == 1);
        Assert.DoesNotContain(scoped.Alerts, x => x.Type == "UnassignedStudents");
    }

    private async Task<Seed> SeedOperationalDashboardAsync()
    {
        var nowUtc = DateTime.UtcNow; var today = DateOnly.FromDateTime(nowUtc.AddHours(3));
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<BorderDbContext>();
        var instructor = new Instructor { FirstName = "Gonca", LastName = "Köprülü", UserId = "dashboard-instructor" };
        var room = new StudioRoom { Name = "Güney", Capacity = 18 };
        var studioClass = new StudioClass { Name = "Hip Hop 1", Instructor = instructor, StudioRoom = room, Capacity = 10, Status = StudioClassStatus.Active, StartDate = today.AddDays(-60) };
        var students = Enumerable.Range(1, 9).Select(index => new Student { FirstName = $"Öğrenci {index}", LastName = "Aktif", Status = StudentStatus.Active, RegistrationDate = today.AddDays(-5) }).ToList();
        var deleted = new Student { FirstName = "Silinmiş", LastName = "Öğrenci", Status = StudentStatus.Active, RegistrationDate = today, IsDeleted = true };
        db.AddRange(instructor, room, studioClass, deleted); db.AddRange(students);
        db.ClassSchedules.Add(new ClassSchedule { StudioClass = studioClass, DayOfWeek = today.DayOfWeek, StartTime = new(0, 30), EndTime = new(1, 30) });
        foreach (var student in students) db.ClassEnrollments.Add(new ClassEnrollment { StudioClass = studioClass, Student = student, StartDate = today.AddDays(-30), Status = EnrollmentStatus.Active });
        var attendedSession = new LessonSession { StudioClass = studioClass, Instructor = instructor, StudioRoom = room, ScheduledStart = IstanbulStart(today.AddDays(-1)).AddHours(10), ScheduledEnd = IstanbulStart(today.AddDays(-1)).AddHours(11), Status = LessonSessionStatus.Completed };
        var missingSession = new LessonSession { StudioClass = studioClass, Instructor = instructor, StudioRoom = room, ScheduledStart = IstanbulStart(today.AddDays(-2)).AddHours(10), ScheduledEnd = IstanbulStart(today.AddDays(-2)).AddHours(11), Status = LessonSessionStatus.Scheduled };
        db.AddRange(attendedSession, missingSession); db.Attendances.Add(new Attendance { LessonSession = attendedSession, Student = students[0], Status = AttendanceStatus.Present, RecordedByUserId = "test-user" });
        var plan = new MembershipPlan { Name = "Aylık", Type = MembershipPlanType.Monthly, DefaultPrice = 1000m, DurationMonths = 1 };
        var membership = new StudentMembership { Student = students[0], MembershipPlan = plan, StartDate = today.AddDays(-20), EndDate = today.AddDays(5), Status = MembershipStatus.Active };
        var invoice = new Invoice { Student = students[0], StudentMembership = membership, Description = "Aylık üyelik", Amount = 1000m, DueDate = today.AddDays(-1), Status = InvoiceStatus.PartiallyPaid };
        db.AddRange(plan, membership, invoice); db.Payments.Add(new Payment { Student = students[0], Invoice = invoice, Amount = 400m, PaymentMethod = PaymentMethod.CreditCard, PaymentDate = IstanbulStart(today).AddHours(10), ReceivedByUserId = "test-user" });
        await db.SaveChangesAsync(); return new(studioClass.Id, today);
    }

    private HttpClient Client(string role, string? userId = null) { var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true }); client.DefaultRequestHeaders.Add("X-Test-Role", role); if (userId is not null) client.DefaultRequestHeaders.Add("X-Test-UserId", userId); return client; }
    private static DateTime IstanbulStart(DateOnly date) => DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue).AddHours(-3), DateTimeKind.Utc);
    private sealed record Seed(Guid ClassId, DateOnly Today);
}
