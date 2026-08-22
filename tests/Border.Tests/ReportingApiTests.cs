using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Border.Application.Operations;
using Border.Domain.Entities;
using Border.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Border.Tests;

public sealed class ReportingApiTests(StudentApiFactory factory) : IClassFixture<StudentApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    [Fact]
    public async Task SummaryAndFinance_ApplyRangeTrendsMethodsBalancesAndIstanbulBoundary()
    {
        await factory.ResetAsync(); var seed = await SeedAsync(); using var client = Client("Management"); var query = Query(seed.From, seed.To);
        var summary = await client.GetFromJsonAsync<ReportingSummaryResponse>($"/api/reports/summary?{query}", JsonOptions);
        Assert.Equal(3m, summary!.ActiveStudents.Value); Assert.Equal(6m, summary.NewStudents.Value); Assert.Equal(500m, summary.NewStudents.TrendPercent);
        Assert.Equal(1m, summary.ActiveMemberships.Value); Assert.Equal(500m, summary.TotalRevenue.Value); Assert.Equal(100m, summary.TotalRevenue.TrendPercent);
        Assert.Equal(900m, summary.OutstandingBalance.Value); Assert.Equal(40m, summary.AttendanceRate.Value); Assert.Equal(-20m, summary.AttendanceRate.TrendPercent);

        var finance = await client.GetFromJsonAsync<ReportingFinanceResponse>($"/api/reports/finance?{query}", JsonOptions);
        Assert.Equal(500m, finance!.Revenue.Total); Assert.Equal(2, finance.Revenue.PaymentCount); Assert.Equal(250m, finance.Revenue.AveragePayment);
        Assert.Contains(finance.Revenue.Methods, x => x.Method == PaymentMethod.Cash && x.Amount == 400m);
        Assert.Contains(finance.Revenue.Methods, x => x.Method == PaymentMethod.CreditCard && x.Amount == 100m);
        Assert.Equal(1300m, finance.Balances.TotalInvoiced); Assert.Equal(400m, finance.Balances.TotalPaid); Assert.Equal(900m, finance.Balances.OutstandingBalance);
        Assert.Equal(900m, finance.Balances.OverdueBalance); Assert.Equal(2, finance.Balances.OverdueInvoiceCount); Assert.Equal(2, finance.Balances.TopDebtors.Count);
        Assert.Contains(finance.Balances.Statuses, x => x.Status == InvoiceStatus.PartiallyPaid && x.Count == 1);
        Assert.Contains(finance.Balances.Statuses, x => x.Status == InvoiceStatus.Pending && x.Count == 1);

        var todayOnly = await client.GetFromJsonAsync<ReportingFinanceResponse>($"/api/reports/finance?{Query(seed.To, seed.To)}", JsonOptions);
        Assert.Equal(100m, todayOnly!.Revenue.Total);
    }

    [Fact]
    public async Task EngagementAndCapacity_ReturnAttendanceOccupancyInstructorAndMembershipAggregates()
    {
        await factory.ResetAsync(); var seed = await SeedAsync(); using var client = Client("Admin"); var query = Query(seed.From, seed.To);
        var engagement = await client.GetFromJsonAsync<ReportingEngagementResponse>($"/api/reports/engagement?{query}", JsonOptions);
        Assert.Equal(7, engagement!.Students.Total); Assert.Equal(3, engagement.Students.Active); Assert.Equal(1, engagement.Students.Trial); Assert.Equal(1, engagement.Students.Frozen); Assert.Equal(1, engagement.Students.Passive); Assert.Equal(1, engagement.Students.Left); Assert.Equal(6, engagement.Students.NewStudents);
        Assert.Equal(5, engagement.Attendance.Total); Assert.Equal(1, engagement.Attendance.Present); Assert.Equal(1, engagement.Attendance.Late); Assert.Equal(1, engagement.Attendance.Absent); Assert.Equal(1, engagement.Attendance.Excused); Assert.Equal(1, engagement.Attendance.MakeUp); Assert.Equal(40m, engagement.Attendance.Rate); Assert.Equal(1, engagement.Attendance.MissingSessions);
        Assert.Equal(seed.ClassId, Assert.Single(engagement.Attendance.Classes).ClassId);

        var capacity = await client.GetFromJsonAsync<ReportingCapacityResponse>($"/api/reports/capacity?{query}", JsonOptions);
        var studioClass = Assert.Single(capacity!.Classes); Assert.Equal(6, studioClass.ActiveStudents); Assert.Equal(60m, studioClass.OccupancyRate);
        var instructor = Assert.Single(capacity.Instructors); Assert.Equal(1, instructor.ActiveClasses); Assert.Equal(6, instructor.TotalStudents); Assert.Equal(2, instructor.Sessions); Assert.Equal(60m, instructor.AverageOccupancy); Assert.Equal(40m, instructor.AttendanceRate);
        Assert.Equal(1, capacity.Memberships.Active); Assert.Equal(1, capacity.Memberships.Frozen); Assert.Equal(1, capacity.Memberships.Expired); Assert.Equal(1, capacity.Memberships.Cancelled);
        var plan = Assert.Single(capacity.Memberships.Plans); Assert.Equal(1, plan.ActiveStudents); Assert.Equal(1000m, plan.TotalInvoiced); Assert.Equal(1, plan.DiscountedMemberships);
        var expiring = Assert.Single(capacity.Memberships.Expiring); Assert.Equal(5, expiring.DaysRemaining);

        var filtered = await client.GetFromJsonAsync<ReportingCapacityResponse>($"/api/reports/capacity?{query}&classId={Guid.NewGuid()}", JsonOptions);
        Assert.Empty(filtered!.Classes); Assert.Empty(filtered.Instructors); Assert.Empty(filtered.Memberships.Plans);
    }

    [Fact]
    public async Task Reports_ValidateDateRangeAndEnforceManagementPolicy()
    {
        await factory.ResetAsync(); using var reception = Client("Reception");
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.GetAsync("/api/reports/summary")).StatusCode);
        using var management = Client("Management");
        Assert.Equal(HttpStatusCode.BadRequest, (await management.GetAsync("/api/reports/summary?from=2026-08-10&to=2026-08-01")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await management.GetAsync("/api/reports/summary?from=2020-01-01&to=2026-08-01")).StatusCode);
    }

    [Fact]
    public async Task Reports_ReturnSafeEmptyAggregatesWhenThereIsNoData()
    {
        await factory.ResetAsync();
        using var client = Client("Management");
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
        var query = Query(today, today);

        var summary = await client.GetFromJsonAsync<ReportingSummaryResponse>($"/api/reports/summary?{query}", JsonOptions);
        var finance = await client.GetFromJsonAsync<ReportingFinanceResponse>($"/api/reports/finance?{query}", JsonOptions);
        var engagement = await client.GetFromJsonAsync<ReportingEngagementResponse>($"/api/reports/engagement?{query}", JsonOptions);
        var capacity = await client.GetFromJsonAsync<ReportingCapacityResponse>($"/api/reports/capacity?{query}", JsonOptions);

        Assert.Equal(0m, summary!.TotalRevenue.Value);
        Assert.Equal(0m, summary.AttendanceRate.Value);
        Assert.Equal(0m, finance!.Balances.OutstandingBalance);
        Assert.Empty(finance.Balances.TopDebtors);
        Assert.Equal(0, engagement!.Attendance.Total);
        Assert.Empty(engagement.Attendance.Classes);
        Assert.Empty(capacity!.Classes);
        Assert.Empty(capacity.Instructors);
        Assert.Empty(capacity.Memberships.Expiring);
    }

    private async Task<Seed> SeedAsync()
    {
        var to = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3)); var from = to.AddDays(-6); var previousRegistration = from.AddDays(-4);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<BorderDbContext>();
        var instructor = new Instructor { FirstName = "Gonca", LastName = "Köprülü" }; var room = new StudioRoom { Name = "Güney", Capacity = 20 }; var studioClass = new StudioClass { Name = "Hip Hop", Instructor = instructor, StudioRoom = room, Capacity = 10, Status = StudioClassStatus.Active, StartDate = from.AddDays(-30) };
        var statuses = new[] { StudentStatus.Active, StudentStatus.Active, StudentStatus.Trial, StudentStatus.Frozen, StudentStatus.Passive, StudentStatus.Left };
        var students = statuses.Select((status,index) => new Student { FirstName = $"Öğrenci {index+1}", LastName = "Rapor", Status = status, RegistrationDate = from.AddDays(index%3) }).ToList();
        var previous = new Student { FirstName = "Önceki", LastName = "Öğrenci", Status = StudentStatus.Active, RegistrationDate = previousRegistration };
        db.AddRange(instructor, room, studioClass, previous); db.AddRange(students); foreach(var student in students) db.ClassEnrollments.Add(new ClassEnrollment { Student = student, StudioClass = studioClass, StartDate = from.AddDays(-1), Status = EnrollmentStatus.Active });
        var completed = new LessonSession { StudioClass = studioClass, Instructor = instructor, StudioRoom = room, ScheduledStart = IstanbulStart(from.AddDays(2)).AddHours(10), ScheduledEnd = IstanbulStart(from.AddDays(2)).AddHours(11), Status = LessonSessionStatus.Completed };
        var missing = new LessonSession { StudioClass = studioClass, Instructor = instructor, StudioRoom = room, ScheduledStart = IstanbulStart(from.AddDays(1)).AddHours(10), ScheduledEnd = IstanbulStart(from.AddDays(1)).AddHours(11), Status = LessonSessionStatus.Scheduled };
        var previousSession = new LessonSession { StudioClass = studioClass, Instructor = instructor, StudioRoom = room, ScheduledStart = IstanbulStart(previousRegistration).AddHours(10), ScheduledEnd = IstanbulStart(previousRegistration).AddHours(11), Status = LessonSessionStatus.Completed };
        db.AddRange(completed, missing, previousSession);
        var attendanceStatuses = new[] { AttendanceStatus.Present, AttendanceStatus.Late, AttendanceStatus.Absent, AttendanceStatus.Excused, AttendanceStatus.MakeUp }; for(var i=0;i<attendanceStatuses.Length;i++) db.Attendances.Add(new Attendance { LessonSession = completed, Student = students[i], Status = attendanceStatuses[i], RecordedByUserId = "test-user" });
        db.Attendances.AddRange(new Attendance { LessonSession = previousSession, Student = students[0], Status = AttendanceStatus.Present, RecordedByUserId = "test-user" }, new Attendance { LessonSession = previousSession, Student = students[1], Status = AttendanceStatus.Absent, RecordedByUserId = "test-user" });
        var plan = new MembershipPlan { Name = "Standart", Type = MembershipPlanType.Monthly, DefaultPrice = 1000m };
        var memberships = new[] { MembershipStatus.Active, MembershipStatus.Frozen, MembershipStatus.Expired, MembershipStatus.Cancelled }.Select((status,index)=>new StudentMembership { Student = students[index], MembershipPlan = plan, StartDate = from.AddDays(-10), EndDate = status==MembershipStatus.Active?to.AddDays(5):to.AddDays(-1), Status = status }).ToList(); db.Add(plan);db.AddRange(memberships);
        foreach(var membership in memberships) db.MembershipPriceHistory.Add(new MembershipPriceHistory { StudentMembership = membership, Price = membership.Status==MembershipStatus.Active?1000m:500m, DiscountAmount = membership.Status==MembershipStatus.Active?100m:null, EffectiveFrom = from.AddDays(-10), ApprovedByUserId = "test-user" });
        var invoiceOne = new Invoice { Student = students[0], StudentMembership = memberships[0], Description = "Üyelik", Amount = 1000m, DueDate = to.AddDays(-1), Status = InvoiceStatus.PartiallyPaid }; var invoiceTwo = new Invoice { Student = students[1], Description = "Ek borç", Amount = 300m, DueDate = to.AddDays(-1), Status = InvoiceStatus.Pending }; db.AddRange(invoiceOne, invoiceTwo);
        db.Payments.AddRange(new Payment { Student = students[0], Invoice = invoiceOne, Amount = 400m, PaymentMethod = PaymentMethod.Cash, PaymentDate = IstanbulStart(from).AddMinutes(30), ReceivedByUserId = "test-user" }, new Payment { Student = students[0], Amount = 100m, PaymentMethod = PaymentMethod.CreditCard, PaymentDate = IstanbulStart(to).AddHours(12), ReceivedByUserId = "test-user" }, new Payment { Student = previous, Amount = 250m, PaymentMethod = PaymentMethod.BankTransfer, PaymentDate = IstanbulStart(previousRegistration).AddHours(12), ReceivedByUserId = "test-user" });
        await db.SaveChangesAsync(); return new(from,to,studioClass.Id);
    }

    private HttpClient Client(string role) { var client=factory.CreateClient(new WebApplicationFactoryClientOptions{HandleCookies=true});client.DefaultRequestHeaders.Add("X-Test-Role",role);return client; }
    private static string Query(DateOnly from,DateOnly to)=>$"from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";
    private static DateTime IstanbulStart(DateOnly date)=>DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue).AddHours(-3),DateTimeKind.Utc);
    private sealed record Seed(DateOnly From,DateOnly To,Guid ClassId);
}
