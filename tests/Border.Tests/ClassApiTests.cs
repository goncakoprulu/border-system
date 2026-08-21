using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Border.Application.Classes;
using Border.Application.Students;
using Border.Domain.Entities;
using Border.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

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
        return new(first.Id, second.Id, instructorOne.Id, roomTwo.Id, studentOne.Id, studentTwo.Id);
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

    private sealed record Seed(Guid FirstClassId, Guid SecondClassId, Guid InstructorOneId, Guid RoomTwoId, Guid StudentOneId, Guid StudentTwoId);
}
