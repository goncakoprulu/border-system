using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Border.Application.Classes;
using Border.Application.Students;
using Border.Domain.Entities;
using Border.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Border.Tests;

public sealed class ClassApiTests(StudentApiFactory factory) : IClassFixture<StudentApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    [Theory]
    [InlineData("Admin", HttpStatusCode.OK)]
    [InlineData("Management", HttpStatusCode.OK)]
    [InlineData("Reception", HttpStatusCode.OK)]
    [InlineData("Instructor", HttpStatusCode.OK)]
    public async Task Directory_AllowsPhase3Roles(string role, HttpStatusCode expected)
    {
        await factory.ResetAsync();
        using var client = Client(role);
        Assert.Equal(expected, (await client.GetAsync("/api/classes")).StatusCode);
    }

    [Fact]
    public async Task Instructor_SeesOnlyAssignedClasses_AndCannotMutate()
    {
        await factory.ResetAsync();
        var seed = await SeedAsync();
        using var client = Client("Instructor", "instructor-one-user");
        var list = await client.GetFromJsonAsync<PagedResponse<ClassListItemResponse>>("/api/classes", JsonOptions);
        Assert.Single(list!.Items);
        Assert.Equal(seed.FirstClassId, list.Items.Single().Id);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/classes/{seed.SecondClassId}")).StatusCode);

        var response = await MutationAsync(client, HttpMethod.Patch, $"/api/classes/{seed.FirstClassId}/status", new ChangeClassStatusRequest(StudioClassStatus.Paused));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ClassCreate_RejectsInvalidSchedule_AndDetectsInstructorConflict()
    {
        await factory.ResetAsync();
        var seed = await SeedAsync();
        using var client = Client("Reception");
        var invalid = Request(seed.InstructorOneId, seed.RoomTwoId, new(DayOfWeek.Tuesday, new(12, 0), new(11, 0)));
        Assert.Equal(HttpStatusCode.BadRequest, (await MutationAsync(client, HttpMethod.Post, "/api/classes", invalid)).StatusCode);

        var conflict = Request(seed.InstructorOneId, seed.RoomTwoId, new(DayOfWeek.Monday, new(10, 30), new(11, 30)));
        var conflictResponse = await MutationAsync(client, HttpMethod.Post, "/api/classes", conflict);
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
        Assert.Contains("eğitmenin", await conflictResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ClassCreate_DetectsStudioRoomConflict()
    {
        await factory.ResetAsync();
        var seed = await SeedAsync();
        using var client = Client("Management");
        var request = Request(seed.InstructorOneId, seed.RoomTwoId, new(DayOfWeek.Wednesday, new(10, 30), new(11, 30)));
        var response = await MutationAsync(client, HttpMethod.Post, "/api/classes", request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("başka bir sınıf", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Enrollment_EnforcesDuplicateAndCapacity_ThenPreservesHistoryInStudent360()
    {
        await factory.ResetAsync();
        var seed = await SeedAsync(capacity: 1);
        using var client = Client("Reception");
        var enroll = await MutationAsync(client, HttpMethod.Post, $"/api/classes/{seed.FirstClassId}/enrollments", new CreateEnrollmentRequest(seed.StudentOneId, new(2026, 8, 1)));
        enroll.EnsureSuccessStatusCode();
        var enrollment = await enroll.Content.ReadFromJsonAsync<ClassEnrollmentResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, (await MutationAsync(client, HttpMethod.Post, $"/api/classes/{seed.FirstClassId}/enrollments", new CreateEnrollmentRequest(seed.StudentOneId, new(2026, 8, 2)))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await MutationAsync(client, HttpMethod.Post, $"/api/classes/{seed.FirstClassId}/enrollments", new CreateEnrollmentRequest(seed.StudentTwoId, new(2026, 8, 2)))).StatusCode);

        var ended = await MutationAsync(client, HttpMethod.Patch, $"/api/classes/{seed.FirstClassId}/enrollments/{enrollment!.Id}/end", new EndEnrollmentRequest(new(2026, 8, 5)));
        ended.EnsureSuccessStatusCode();
        var student = await client.GetFromJsonAsync<StudentDetailResponse>($"/api/students/{seed.StudentOneId}", JsonOptions);
        var history = Assert.Single(student!.ClassEnrollments);
        Assert.Equal(EnrollmentStatus.Completed, history.Status);
        Assert.Equal(new DateOnly(2026, 8, 5), history.EndDate);
    }

    [Fact]
    public async Task ArchivedClass_IsHidden_AndOnlyManagementCanIncludeIt()
    {
        await factory.ResetAsync();
        var seed = await SeedAsync();
        using var management = Client("Management");
        Assert.Equal(HttpStatusCode.NoContent, (await MutationAsync(management, HttpMethod.Delete, $"/api/classes/{seed.FirstClassId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await management.GetAsync($"/api/classes/{seed.FirstClassId}")).StatusCode);
        var archived = await management.GetFromJsonAsync<PagedResponse<ClassListItemResponse>>("/api/classes?includeArchived=true", JsonOptions);
        Assert.Contains(archived!.Items, x => x.Id == seed.FirstClassId && x.IsArchived);

        using var reception = Client("Reception");
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.GetAsync("/api/classes?includeArchived=true")).StatusCode);
    }

    [Fact]
    public async Task ClassUpdate_ReplacesMultipleSchedulesAtomically()
    {
        await factory.ResetAsync();
        var seed = await SeedAsync();
        using var client = Client("Reception");
        var request = new StudioClassUpsertRequest("Bale Güncellendi", null, seed.InstructorOneId, seed.RoomOneId, 12, "Orta", null, StudioClassStatus.Active, new(2026, 1, 1), null,
            [new(DayOfWeek.Tuesday, new(18, 0), new(19, 0)), new(DayOfWeek.Thursday, new(20, 15), new(21, 30))]);

        var response = await MutationAsync(client, HttpMethod.Put, $"/api/classes/{seed.FirstClassId}", request);
        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<ClassDetailResponse>(JsonOptions);
        Assert.Equal("Bale Güncellendi", updated!.Name);
        Assert.Equal(2, updated.Schedules.Count);
        Assert.DoesNotContain(updated.Schedules, x => x.DayOfWeek == DayOfWeek.Monday);
    }

    [Fact]
    public async Task ClassUpdate_ReturnsFieldValidationAndRoomCapacityErrors()
    {
        await factory.ResetAsync();
        var seed = await SeedAsync();
        using var client = Client("Management");
        var invalid = new StudioClassUpsertRequest(" ", null, Guid.Empty, Guid.Empty, 0, null, null, StudioClassStatus.Active, default, new(2025, 1, 1),
            [new(DayOfWeek.Tuesday, new(12, 0), new(11, 0))]);
        var invalidResponse = await MutationAsync(client, HttpMethod.Put, $"/api/classes/{seed.FirstClassId}", invalid);
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        var invalidBody = await invalidResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(invalidBody.GetProperty("errors").TryGetProperty("Name", out _));
        Assert.True(invalidBody.GetProperty("errors").TryGetProperty("Schedules", out _));

        var duplicateSchedule = Request(seed.InstructorOneId, seed.RoomOneId, new(DayOfWeek.Tuesday, new(12, 0), new(13, 0))) with
        {
            Schedules = [new(DayOfWeek.Tuesday, new(12, 0), new(13, 0)), new(DayOfWeek.Tuesday, new(12, 0), new(13, 0))]
        };
        var duplicateResponse = await MutationAsync(client, HttpMethod.Put, $"/api/classes/{seed.FirstClassId}", duplicateSchedule);
        Assert.Equal(HttpStatusCode.BadRequest, duplicateResponse.StatusCode);

        var capacityRequest = Request(seed.InstructorOneId, seed.RoomOneId, new(DayOfWeek.Tuesday, new(12, 0), new(13, 0))) with { Capacity = 21 };
        var capacityResponse = await MutationAsync(client, HttpMethod.Put, $"/api/classes/{seed.FirstClassId}", capacityRequest);
        Assert.Equal(HttpStatusCode.Conflict, capacityResponse.StatusCode);
        Assert.Contains("salon kapasitesinden", await capacityResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ClassUpdate_DetectsRoomAndInstructorConflicts_AndEnforcesRole()
    {
        await factory.ResetAsync();
        var seed = await SeedAsync();
        using var management = Client("Management");
        var roomConflict = Request(seed.InstructorOneId, seed.RoomTwoId, new(DayOfWeek.Wednesday, new(10, 30), new(11, 30)));
        Assert.Equal(HttpStatusCode.Conflict, (await MutationAsync(management, HttpMethod.Put, $"/api/classes/{seed.FirstClassId}", roomConflict)).StatusCode);
        var instructorConflict = Request(seed.InstructorTwoId, seed.RoomOneId, new(DayOfWeek.Wednesday, new(10, 30), new(11, 30)));
        Assert.Equal(HttpStatusCode.Conflict, (await MutationAsync(management, HttpMethod.Put, $"/api/classes/{seed.FirstClassId}", instructorConflict)).StatusCode);

        using var instructor = Client("Instructor", "instructor-one-user");
        Assert.Equal(HttpStatusCode.Forbidden, (await MutationAsync(instructor, HttpMethod.Put, $"/api/classes/{seed.FirstClassId}", roomConflict)).StatusCode);
    }

    [Fact]
    public async Task ClassUpdate_UnexpectedServiceFailure_Returns500WithoutExceptionText()
    {
        await factory.ResetAsync();
        using var application = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IClassService>();
            services.AddSingleton(DispatchProxy.Create<IClassService, ThrowingClassServiceProxy>());
        }));
        using var client = application.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        client.DefaultRequestHeaders.Add("X-Test-Role", "Management");
        var request = Request(Guid.NewGuid(), Guid.NewGuid(), new(DayOfWeek.Tuesday, new(12, 0), new(13, 0)));

        var response = await MutationAsync(client, HttpMethod.Put, $"/api/classes/{Guid.NewGuid()}", request);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("database-password", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ScheduleDay_UsesNumericContract_ForEditSaveReloadAndCreate()
    {
        await factory.ResetAsync();
        var seed = await SeedAsync();
        using var client = Client("Management");
        var editRequest = new
        {
            name = "Çarşamba Sınıfı", description = (string?)null, instructorId = seed.InstructorOneId, studioRoomId = seed.RoomOneId,
            capacity = 10, level = (string?)null, ageGroup = (string?)null, status = "Active", startDate = "2026-01-01", endDate = (string?)null,
            schedules = new[] { new { dayOfWeek = 3, startTime = "19:00:00", endTime = "20:15:00" } }
        };
        var edit = await MutationAsync(client, HttpMethod.Put, $"/api/classes/{seed.FirstClassId}", editRequest);
        edit.EnsureSuccessStatusCode();
        var editJson = await edit.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Number, editJson.GetProperty("schedules")[0].GetProperty("dayOfWeek").ValueKind);
        Assert.Equal(3, editJson.GetProperty("schedules")[0].GetProperty("dayOfWeek").GetInt32());

        var reload = await client.GetFromJsonAsync<JsonElement>($"/api/classes/{seed.FirstClassId}");
        Assert.Equal(3, reload.GetProperty("schedules")[0].GetProperty("dayOfWeek").GetInt32());
        var list = await client.GetFromJsonAsync<JsonElement>("/api/classes?pageSize=100");
        var listedClass = list.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == seed.FirstClassId);
        Assert.Equal(3, listedClass.GetProperty("schedules")[0].GetProperty("dayOfWeek").GetInt32());

        var createRequest = new
        {
            name = "Perşembe Sınıfı", description = (string?)null, instructorId = seed.InstructorOneId, studioRoomId = seed.RoomOneId,
            capacity = 10, level = (string?)null, ageGroup = (string?)null, status = "Active", startDate = "2026-01-01", endDate = (string?)null,
            schedules = new[] { new { dayOfWeek = 4, startTime = "20:15:00", endTime = "21:30:00" } }
        };
        var create = await MutationAsync(client, HttpMethod.Post, "/api/classes", createRequest);
        create.EnsureSuccessStatusCode();
        var createJson = await create.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(4, createJson.GetProperty("schedules")[0].GetProperty("dayOfWeek").GetInt32());
    }

    private async Task<Seed> SeedAsync(int capacity = 10)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BorderDbContext>();
        var instructorOne = new Instructor { FirstName = "Ada", LastName = "Eğitmen", UserId = "instructor-one-user" };
        var instructorTwo = new Instructor { FirstName = "Efe", LastName = "Eğitmen", UserId = "instructor-two-user" };
        var roomOne = new StudioRoom { Name = "Salon A", Capacity = 20 };
        var roomTwo = new StudioRoom { Name = "Salon B", Capacity = 20 };
        var studentOne = new Student { FirstName = "Duru", LastName = "Ak", Status = StudentStatus.Active, RegistrationDate = new(2026, 1, 1) };
        var studentTwo = new Student { FirstName = "Can", LastName = "Ak", Status = StudentStatus.Active, RegistrationDate = new(2026, 1, 1) };
        var first = new StudioClass { Name = "Bale 1", Instructor = instructorOne, StudioRoom = roomOne, Capacity = capacity, Status = StudioClassStatus.Active, StartDate = new(2026, 1, 1) };
        var second = new StudioClass { Name = "Bale 2", Instructor = instructorTwo, StudioRoom = roomTwo, Capacity = 10, Status = StudioClassStatus.Active, StartDate = new(2026, 1, 1) };
        db.AddRange(instructorOne, instructorTwo, roomOne, roomTwo, studentOne, studentTwo, first, second);
        db.ClassSchedules.AddRange(
            new ClassSchedule { StudioClass = first, DayOfWeek = DayOfWeek.Monday, StartTime = new(10, 0), EndTime = new(11, 0) },
            new ClassSchedule { StudioClass = second, DayOfWeek = DayOfWeek.Wednesday, StartTime = new(10, 0), EndTime = new(11, 0) });
        await db.SaveChangesAsync();
        return new(first.Id, second.Id, instructorOne.Id, instructorTwo.Id, roomOne.Id, roomTwo.Id, studentOne.Id, studentTwo.Id);
    }

    private HttpClient Client(string role, string? userId = null)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        if (userId is not null) client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
        return client;
    }

    private static StudioClassUpsertRequest Request(Guid instructorId, Guid roomId, ClassScheduleRequest schedule) =>
        new("Yeni Sınıf", null, instructorId, roomId, 10, null, null, StudioClassStatus.Active, new(2026, 1, 1), null, [schedule]);

    private static async Task<HttpResponseMessage> MutationAsync(HttpClient client, HttpMethod method, string path, object? body = null)
    {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-XSRF-TOKEN", csrf.GetProperty("token").GetString());
        if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
        return await client.SendAsync(request);
    }

    private sealed record Seed(Guid FirstClassId, Guid SecondClassId, Guid InstructorOneId, Guid InstructorTwoId, Guid RoomOneId, Guid RoomTwoId, Guid StudentOneId, Guid StudentTwoId);

    public class ThrowingClassServiceProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new InvalidOperationException("database-password must never reach the client");
    }
}
