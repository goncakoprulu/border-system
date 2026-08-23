using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Border.Application.Classes;
using Border.Application.Operations;
using Border.Domain.Entities;
using Border.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Border.Tests;

public sealed class OperationsApiTests(StudentApiFactory factory) : IClassFixture<StudentApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    [Fact]
    public async Task Schedule_ReturnsRealClassScheduleData()
    {
        await factory.ResetAsync(); var seed = await SeedAsync(); using var client = Client("Reception");
        var items = await client.GetFromJsonAsync<IReadOnlyCollection<ScheduleItemResponse>>("/api/schedule?day=Monday", JsonOptions);
        var item = Assert.Single(items!); Assert.Equal(seed.ClassId, item.ClassId); Assert.Equal("Kuzey", item.RoomName); Assert.Equal(new TimeOnly(10, 0), item.StartTime);
        var raw = await client.GetFromJsonAsync<JsonElement>("/api/schedule?day=1");
        Assert.Equal(JsonValueKind.Number, raw[0].GetProperty("dayOfWeek").ValueKind);
        Assert.Equal(1, raw[0].GetProperty("dayOfWeek").GetInt32());
    }

    [Fact]
    public async Task Attendance_CreatesThenUpdatesOneRecordPerStudent()
    {
        await factory.ResetAsync(); var seed = await SeedAsync(); using var client = Client("Instructor", "instructor-user");
        var first = await Mutation(client, HttpMethod.Put, $"/api/attendance/sessions/{seed.SessionId}", new SaveAttendanceRequest([new(seed.StudentId, AttendanceStatus.Present, null)])); first.EnsureSuccessStatusCode();
        var second = await Mutation(client, HttpMethod.Put, $"/api/attendance/sessions/{seed.SessionId}", new SaveAttendanceRequest([new(seed.StudentId, AttendanceStatus.Late, "Trafik")])); second.EnsureSuccessStatusCode();
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<BorderDbContext>(); var attendance = Assert.Single(db.Attendances); Assert.Equal(AttendanceStatus.Late, attendance.Status); Assert.Equal("Trafik", attendance.Notes); Assert.Equal(LessonSessionStatus.Completed, db.LessonSessions.Single().Status);
    }

    [Fact]
    public async Task AttendanceSessions_MaterializeWeeklyScheduleOnlyOnce_AndApplyFilters()
    {
        await factory.ResetAsync(); var seed = await SeedAsync(); using var client = Client("Reception");
        var date = new DateOnly(2026, 8, 24);
        var first = await client.GetFromJsonAsync<IReadOnlyCollection<SessionListItemResponse>>($"/api/attendance/sessions?date={date:yyyy-MM-dd}", JsonOptions);
        var created = Assert.Single(first!); Assert.Equal(seed.ClassId, created.ClassId); Assert.Equal(seed.InstructorId, created.InstructorId); Assert.Equal(seed.RoomId, created.RoomId); Assert.Equal(1, created.StudentCount);
        var second = await client.GetFromJsonAsync<IReadOnlyCollection<SessionListItemResponse>>($"/api/attendance/sessions?date={date:yyyy-MM-dd}&classId={seed.ClassId}&instructorId={seed.InstructorId}&roomId={seed.RoomId}", JsonOptions);
        Assert.Single(second!);
        var filtered = await client.GetFromJsonAsync<IReadOnlyCollection<SessionListItemResponse>>($"/api/attendance/sessions?date={date:yyyy-MM-dd}&roomId={Guid.NewGuid()}", JsonOptions);
        Assert.Empty(filtered!);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<BorderDbContext>();
        Assert.Equal(2, await db.LessonSessions.CountAsync());
    }

    [Fact]
    public async Task Attendance_LoadsOnlyActiveEnrollments_AndRejectsIncompleteSave()
    {
        await factory.ResetAsync(); var seed = await SeedAsync();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BorderDbContext>();
            var inactive = new Student { FirstName = "Pasif", LastName = "Öğrenci", Status = StudentStatus.Active, RegistrationDate = new(2026, 1, 1) };
            db.Add(inactive); db.Add(new ClassEnrollment { StudioClassId = seed.ClassId, Student = inactive, StartDate = new(2026, 1, 1), Status = EnrollmentStatus.Completed }); await db.SaveChangesAsync();
        }
        using var client = Client("Reception");
        var detail = await client.GetFromJsonAsync<AttendanceDetailResponse>($"/api/attendance/sessions/{seed.SessionId}", JsonOptions);
        Assert.Single(detail!.Students); Assert.Equal(seed.StudentId, detail.Students.Single().StudentId);
        var incomplete = await Mutation(client, HttpMethod.Put, $"/api/attendance/sessions/{seed.SessionId}", new SaveAttendanceRequest([]));
        Assert.Equal(HttpStatusCode.BadRequest, incomplete.StatusCode);
    }

    [Fact]
    public async Task Attendance_EmptyClassCanBeCompletedWithoutDuplicateRows()
    {
        await factory.ResetAsync(); var seed = await SeedAsync();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BorderDbContext>(); var enrollment = await db.ClassEnrollments.SingleAsync(); enrollment.Status = EnrollmentStatus.Completed; await db.SaveChangesAsync();
        }
        using var client = Client("Reception");
        var response = await Mutation(client, HttpMethod.Put, $"/api/attendance/sessions/{seed.SessionId}", new SaveAttendanceRequest([])); response.EnsureSuccessStatusCode();
        await using var verifyScope = factory.Services.CreateAsyncScope(); var verify = verifyScope.ServiceProvider.GetRequiredService<BorderDbContext>(); Assert.Empty(verify.Attendances); Assert.Equal(LessonSessionStatus.Completed, (await verify.LessonSessions.SingleAsync()).Status);
    }

    [Fact]
    public async Task MembershipPaymentAndBalances_UseInvoiceLedger()
    {
        await factory.ResetAsync(); var seed = await SeedAsync(); using var client = Client("Reception");
        var membershipResponse = await Mutation(client, HttpMethod.Post, "/api/memberships", new CreateMembershipRequest(seed.StudentId, seed.PlanId, new DateOnly(2026, 8, 1), null, null, null, null)); membershipResponse.EnsureSuccessStatusCode();
        var invoices = await client.GetFromJsonAsync<IReadOnlyCollection<InvoiceOptionResponse>>($"/api/students/{seed.StudentId}/open-invoices", JsonOptions); var invoice = Assert.Single(invoices!); Assert.Equal(1000m, invoice.Remaining);
        var paymentResponse = await Mutation(client, HttpMethod.Post, "/api/payments", new CreatePaymentRequest(seed.StudentId, invoice.Id, 400m, PaymentMethod.CreditCard, new DateTime(2026, 8, 10, 9, 0, 0, DateTimeKind.Utc), null)); paymentResponse.EnsureSuccessStatusCode();
        var balances = await client.GetFromJsonAsync<BalancesResponse>("/api/balances", JsonOptions); var balance = Assert.Single(balances!.Items); Assert.Equal(1000m, balance.TotalDebt); Assert.Equal(400m, balance.Paid); Assert.Equal(600m, balance.Remaining);
    }

    [Fact]
    public async Task StudentWorkspace_ReturnsFinanceAndAttendanceHistory()
    {
        await factory.ResetAsync(); var seed = await SeedAsync(); using var client = Client("Reception");
        var membership = await Mutation(client, HttpMethod.Post, "/api/memberships", new CreateMembershipRequest(seed.StudentId, seed.PlanId, new(2026, 8, 1), null, 900m, 100m, "Erken kayıt"));
        membership.EnsureSuccessStatusCode();
        var invoice = Assert.Single((await client.GetFromJsonAsync<IReadOnlyCollection<InvoiceOptionResponse>>($"/api/students/{seed.StudentId}/open-invoices", JsonOptions))!);
        (await Mutation(client, HttpMethod.Post, "/api/payments", new CreatePaymentRequest(seed.StudentId, invoice.Id, 400m, PaymentMethod.CreditCard, new DateTime(2026, 8, 10, 9, 0, 0, DateTimeKind.Utc), "İlk ödeme"))).EnsureSuccessStatusCode();
        (await Mutation(client, HttpMethod.Put, $"/api/attendance/sessions/{seed.SessionId}", new SaveAttendanceRequest([new(seed.StudentId, AttendanceStatus.Late, "Trafik")]))).EnsureSuccessStatusCode();

        var finance = await client.GetFromJsonAsync<StudentFinanceOverviewResponse>($"/api/students/{seed.StudentId}/finance-overview", JsonOptions);
        Assert.Equal(800m, finance!.TotalInvoiced); Assert.Equal(400m, finance.TotalPaid); Assert.Equal(400m, finance.OpenBalance);
        Assert.Equal(100m, Assert.Single(finance.Memberships).DiscountAmount); Assert.Single(finance.Invoices); Assert.Single(finance.Payments);
        var attendance = await client.GetFromJsonAsync<StudentAttendanceHistoryResponse>($"/api/students/{seed.StudentId}/attendance-history", JsonOptions);
        Assert.Equal(1, attendance!.Total); Assert.Equal(1, attendance.Late); Assert.Equal(100m, attendance.AttendanceRate); Assert.Equal("Trafik", Assert.Single(attendance.Items).Notes);

        (await Mutation(client, HttpMethod.Post, "/api/payments", new CreatePaymentRequest(seed.StudentId, invoice.Id, 400m, PaymentMethod.Cash, new DateTime(2026, 8, 11, 9, 0, 0, DateTimeKind.Utc), "Kalan ödeme"))).EnsureSuccessStatusCode();
        var paidFinance = await client.GetFromJsonAsync<StudentFinanceOverviewResponse>($"/api/students/{seed.StudentId}/finance-overview", JsonOptions);
        Assert.Equal(800m, paidFinance!.TotalPaid); Assert.Equal(0m, paidFinance.OpenBalance); Assert.Equal(2, paidFinance.Payments.Count);

        var filteredSessions = await client.GetFromJsonAsync<IReadOnlyCollection<SessionListItemResponse>>($"/api/attendance/sessions?date=2026-08-22&studentId={seed.StudentId}", JsonOptions);
        Assert.Single(filteredSessions!);
    }

    [Fact]
    public async Task StudentWorkspace_EmptyHistoryAndPermissions_AreHandled()
    {
        await factory.ResetAsync(); var seed = await SeedAsync(); using var reception = Client("Reception");
        var finance = await reception.GetFromJsonAsync<StudentFinanceOverviewResponse>($"/api/students/{seed.StudentId}/finance-overview", JsonOptions);
        var attendance = await reception.GetFromJsonAsync<StudentAttendanceHistoryResponse>($"/api/students/{seed.StudentId}/attendance-history", JsonOptions);
        Assert.Empty(finance!.Memberships); Assert.Empty(finance.Invoices); Assert.Empty(finance.Payments); Assert.Equal(0m, finance.OpenBalance);
        Assert.Empty(attendance!.Items); Assert.Equal(0m, attendance.AttendanceRate);
        using var instructor = Client("Instructor", "instructor-user");
        Assert.Equal(HttpStatusCode.Forbidden, (await instructor.GetAsync($"/api/students/{seed.StudentId}/finance-overview")).StatusCode);
    }

    [Fact]
    public async Task Memberships_RejectOverlap_AndSupportFreezeReactivateCancelWithoutLosingFinance()
    {
        await factory.ResetAsync(); var seed = await SeedAsync(); using var client = Client("Reception");
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
        var createdResponse = await Mutation(client, HttpMethod.Post, "/api/memberships", new CreateMembershipRequest(seed.StudentId, seed.PlanId, today, today.AddMonths(1), 900m, 100m, "Sadakat indirimi"));
        createdResponse.EnsureSuccessStatusCode();
        var created = await createdResponse.Content.ReadFromJsonAsync<MembershipListItemResponse>(JsonOptions);

        var overlap = await Mutation(client, HttpMethod.Post, "/api/memberships", new CreateMembershipRequest(seed.StudentId, seed.PlanId, today.AddDays(1), today.AddMonths(2), null, null, null));
        Assert.Equal(HttpStatusCode.BadRequest, overlap.StatusCode);

        var frozenResponse = await Mutation(client, HttpMethod.Patch, $"/api/memberships/{created!.Id}/status", new ChangeMembershipStatusRequest(MembershipStatus.Frozen));
        frozenResponse.EnsureSuccessStatusCode();
        Assert.Equal(MembershipStatus.Frozen, (await frozenResponse.Content.ReadFromJsonAsync<MembershipListItemResponse>(JsonOptions))!.Status);

        var activeResponse = await Mutation(client, HttpMethod.Patch, $"/api/memberships/{created.Id}/status", new ChangeMembershipStatusRequest(MembershipStatus.Active));
        activeResponse.EnsureSuccessStatusCode();
        Assert.Equal(MembershipStatus.Active, (await activeResponse.Content.ReadFromJsonAsync<MembershipListItemResponse>(JsonOptions))!.Status);

        var cancelledResponse = await Mutation(client, HttpMethod.Patch, $"/api/memberships/{created.Id}/status", new ChangeMembershipStatusRequest(MembershipStatus.Cancelled));
        cancelledResponse.EnsureSuccessStatusCode();
        Assert.Equal(MembershipStatus.Cancelled, (await cancelledResponse.Content.ReadFromJsonAsync<MembershipListItemResponse>(JsonOptions))!.Status);
        var finance = await client.GetFromJsonAsync<StudentFinanceOverviewResponse>($"/api/students/{seed.StudentId}/finance-overview", JsonOptions);
        Assert.Single(finance!.Invoices); Assert.Equal(800m, finance.OpenBalance);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BorderDbContext>(); var membership = await db.StudentMemberships.SingleAsync(x => x.Id == created.Id);
            membership.Status = MembershipStatus.Active; membership.StartDate = today.AddMonths(-1); membership.EndDate = today.AddDays(-1); await db.SaveChangesAsync();
        }
        var expired = await client.GetFromJsonAsync<IReadOnlyCollection<MembershipListItemResponse>>("/api/memberships?status=Expired", JsonOptions);
        Assert.Equal(MembershipStatus.Expired, Assert.Single(expired!).Status);
    }

    [Fact]
    public async Task AttendanceDetail_ReturnsStudentNotesAndLastFourAbsenceSignal()
    {
        await factory.ResetAsync(); var seed = await SeedAsync();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BorderDbContext>();
            var student = await db.Students.SingleAsync(x => x.Id == seed.StudentId); student.Notes = "Diz hassasiyeti var.";
            var baseSession = await db.LessonSessions.SingleAsync(x => x.Id == seed.SessionId);
            foreach (var index in Enumerable.Range(1, 3))
            {
                var session = new LessonSession { StudioClassId = seed.ClassId, InstructorId = seed.InstructorId, StudioRoomId = seed.RoomId, ScheduledStart = baseSession.ScheduledStart.AddDays(-index), ScheduledEnd = baseSession.ScheduledEnd.AddDays(-index), Status = LessonSessionStatus.Completed };
                db.LessonSessions.Add(session);
                db.Attendances.Add(new Attendance { LessonSession = session, StudentId = seed.StudentId, Status = index <= 2 ? AttendanceStatus.Absent : AttendanceStatus.Present, RecordedByUserId = "test-user" });
            }
            await db.SaveChangesAsync();
            Assert.Equal(3, await db.Attendances.CountAsync(x => x.StudentId == seed.StudentId && x.LessonSessionId != seed.SessionId));
        }
        using var client = Client("Instructor", "instructor-user");
        var detail = await client.GetFromJsonAsync<AttendanceDetailResponse>($"/api/attendance/sessions/{seed.SessionId}", JsonOptions);
        var studentRow = Assert.Single(detail!.Students);
        Assert.Equal("Diz hassasiyeti var.", studentRow.StudentNotes);
        Assert.Equal(3, studentRow.RecentSessionCount);
        Assert.Equal(2, studentRow.RecentAbsenceCount);
    }

    [Fact]
    public async Task GlobalSearch_FindsOperationalRecords_AndScopesInstructorStudents()
    {
        await factory.ResetAsync(); await SeedAsync();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BorderDbContext>();
            var otherInstructor = new Instructor { FirstName = "Başka", LastName = "Eğitmen", UserId = "other-user" };
            var otherRoom = new StudioRoom { Name = "Gizli Salon", Capacity = 10 };
            var otherStudent = new Student { FirstName = "Gizli", LastName = "Öğrenci", Status = StudentStatus.Active, RegistrationDate = new(2026, 1, 1) };
            var otherClass = new StudioClass { Name = "Gizli Ders", Instructor = otherInstructor, StudioRoom = otherRoom, Capacity = 10, Status = StudioClassStatus.Active, StartDate = new(2026, 1, 1) };
            db.AddRange(otherInstructor, otherRoom, otherStudent, otherClass, new ClassEnrollment { StudioClass = otherClass, Student = otherStudent, StartDate = new(2026, 1, 1), Status = EnrollmentStatus.Active }); await db.SaveChangesAsync();
        }
        using var management = Client("Management");
        var managementResult = await management.GetFromJsonAsync<GlobalSearchResponse>("/api/search?q=Gizli", JsonOptions);
        Assert.Contains(managementResult!.Items, x => x.Type == "Student" && x.Label == "Gizli Öğrenci");
        Assert.Contains(managementResult.Items, x => x.Type == "Class" && x.Label == "Gizli Ders");

        using var instructor = Client("Instructor", "instructor-user");
        var own = await instructor.GetFromJsonAsync<GlobalSearchResponse>("/api/search?q=Duru", JsonOptions);
        Assert.Contains(own!.Items, x => x.Type == "Student" && x.Label == "Duru Ak");
        var hidden = await instructor.GetFromJsonAsync<GlobalSearchResponse>("/api/search?q=Gizli", JsonOptions);
        Assert.Empty(hidden!.Items);
    }

    [Theory]
    [InlineData("Instructor", "/api/payments", HttpStatusCode.Forbidden)]
    [InlineData("Reception", "/api/reports", HttpStatusCode.Forbidden)]
    [InlineData("Management", "/api/users", HttpStatusCode.Forbidden)]
    [InlineData("Member", "/api/attendance/sessions", HttpStatusCode.Forbidden)]
    [InlineData("Member", "/api/search?q=Duru", HttpStatusCode.Forbidden)]
    [InlineData("Instructor", "/api/memberships", HttpStatusCode.Forbidden)]
    [InlineData("Admin", "/api/users", HttpStatusCode.OK)]
    public async Task SensitiveModules_EnforceExistingRoles(string role, string path, HttpStatusCode expected)
    { await factory.ResetAsync(); using var client = Client(role); Assert.Equal(expected, (await client.GetAsync(path)).StatusCode); }

    [Fact]
    public async Task InstructorCrud_UsesExistingEndpointAndValidation()
    {
        await factory.ResetAsync(); using var client = Client("Management");
        var create = await Mutation(client, HttpMethod.Post, "/api/instructors", new InstructorUpsertRequest("Ada", "Yılmaz", "05320000000", "ada@example.com", null)); create.EnsureSuccessStatusCode(); var instructor = await create.Content.ReadFromJsonAsync<InstructorResponse>(JsonOptions);
        var update = await Mutation(client, HttpMethod.Put, $"/api/instructors/{instructor!.Id}", new InstructorUpsertRequest("Ada", "Demir", null, "ada@example.com", null)); update.EnsureSuccessStatusCode(); Assert.Equal("Demir", (await update.Content.ReadFromJsonAsync<InstructorResponse>(JsonOptions))!.LastName);
        var archive = await Mutation(client, HttpMethod.Delete, $"/api/instructors/{instructor.Id}", new { }); Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);
        var records = await client.GetFromJsonAsync<IReadOnlyCollection<InstructorResponse>>("/api/instructors", JsonOptions); Assert.Empty(records!);
    }

    private async Task<Seed> SeedAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<BorderDbContext>();
        var instructor = new Instructor { FirstName = "Ada", LastName = "Eğitmen", UserId = "instructor-user" }; var room = new StudioRoom { Name = "Kuzey", Capacity = 20 }; var student = new Student { FirstName = "Duru", LastName = "Ak", Status = StudentStatus.Active, RegistrationDate = new(2026, 1, 1) }; var studioClass = new StudioClass { Name = "Bale", Instructor = instructor, StudioRoom = room, Capacity = 12, Status = StudioClassStatus.Active, StartDate = new(2026, 1, 1), Level = "Başlangıç" }; var schedule = new ClassSchedule { StudioClass = studioClass, DayOfWeek = DayOfWeek.Monday, StartTime = new(10, 0), EndTime = new(11, 0) }; var enrollment = new ClassEnrollment { StudioClass = studioClass, Student = student, StartDate = new(2026, 1, 1), Status = EnrollmentStatus.Active }; var session = new LessonSession { StudioClass = studioClass, Instructor = instructor, StudioRoom = room, ScheduledStart = new DateTime(2026, 8, 22, 7, 0, 0, DateTimeKind.Utc), ScheduledEnd = new DateTime(2026, 8, 22, 8, 0, 0, DateTimeKind.Utc) }; var plan = new MembershipPlan { Name = "Aylık", Type = MembershipPlanType.Monthly, DefaultPrice = 1000m, DurationMonths = 1 };
        db.AddRange(instructor, room, student, studioClass, schedule, enrollment, session, plan); await db.SaveChangesAsync(); return new(studioClass.Id, student.Id, session.Id, plan.Id, instructor.Id, room.Id);
    }
    private HttpClient Client(string role, string? userId = null) { var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true }); client.DefaultRequestHeaders.Add("X-Test-Role", role); if (userId is not null) client.DefaultRequestHeaders.Add("X-Test-UserId", userId); return client; }
    private static async Task<HttpResponseMessage> Mutation(HttpClient client, HttpMethod method, string path, object body) { var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf"); using var request = new HttpRequestMessage(method, path); request.Headers.Add("X-XSRF-TOKEN", csrf.GetProperty("token").GetString()); request.Content = JsonContent.Create(body, options: JsonOptions); return await client.SendAsync(request); }
    private sealed record Seed(Guid ClassId, Guid StudentId, Guid SessionId, Guid PlanId, Guid InstructorId, Guid RoomId);
}
