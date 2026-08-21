using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Border.Application.Auditing;
using Border.Application.Students;
using Border.Domain.Entities;
using Border.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Border.Tests;

public sealed class StudentApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=test;Database=test;Username=test;Password=test");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<BorderDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<BorderDbContext>>();
            services.RemoveAll<BorderDbContext>();
            services.AddDbContext<BorderDbContext>(options => options.UseInMemoryDatabase("student-api-tests"));
            services.RemoveAll<IAuditWriter>();
            services.AddScoped<IAuditWriter, TestAuditWriter>();
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultForbidScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    public async Task ResetAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BorderDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }

    private sealed class TestAuditWriter : IAuditWriter
    {
        public Task WriteAsync(string action, string entityType, string entityId, object? oldValues, object? newValues, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role = Request.Headers["X-Test-Role"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(role)) return Task.FromResult(AuthenticateResult.NoResult());
        var userId = Request.Headers["X-Test-UserId"].FirstOrDefault() ?? "test-user";
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Name, "Test User"), new Claim(ClaimTypes.Role, role) };
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName)), SchemeName)));
    }
}

public sealed class StudentApiTests(StudentApiFactory factory) : IClassFixture<StudentApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    [Theory]
    [InlineData("Admin", HttpStatusCode.OK)]
    [InlineData("Management", HttpStatusCode.OK)]
    [InlineData("Reception", HttpStatusCode.OK)]
    [InlineData("Instructor", HttpStatusCode.Forbidden)]
    public async Task StudentDirectory_EnforcesRolePolicy(string role, HttpStatusCode expected)
    {
        await factory.ResetAsync();
        using var client = CreateClient(role);
        var response = await client.GetAsync("/api/students");
        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Create_ValidatesRequiredFields()
    {
        await factory.ResetAsync();
        using var client = CreateClient("Reception");
        var response = await SendMutationAsync(client, HttpMethod.Post, "/api/students", new StudentUpsertRequest(" ", "", null, "bad-email", null, null, null, StudentStatus.Active, default));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_Get_Update_Filter_Paginate_Duplicate_AndArchive_WorkTogether()
    {
        await factory.ResetAsync();
        using var client = CreateClient("Management");
        var first = await CreateStudentAsync(client, "Ayşe", "Yılmaz", "0532 111 22 33", "AYSE@EXAMPLE.COM");
        Assert.Empty(first.DuplicateWarnings);
        Assert.Equal("05321112233", first.Student.Phone);
        Assert.Equal("ayse@example.com", first.Student.Email);

        var duplicate = await CreateStudentAsync(client, "Başka", "Kişi", "(0532) 111-22-33", null);
        Assert.Single(duplicate.DuplicateWarnings);
        Assert.Equal("phone", duplicate.DuplicateWarnings.Single().MatchedOn);

        var list = await client.GetFromJsonAsync<PagedResponse<StudentListItemResponse>>("/api/students?search=Ayşe%20Yılmaz&page=1&pageSize=1", JsonOptions);
        Assert.NotNull(list);
        Assert.Single(list.Items);
        Assert.Equal(1, list.PageSize);
        Assert.Equal(1, list.TotalCount);

        var update = new StudentUpsertRequest("Ayşe", "Yıldız", "05321112233", "ayse@example.com", new DateOnly(2000, 1, 2), "Kadın", "Güncellendi", StudentStatus.Frozen, new DateOnly(2026, 1, 1));
        var updateResponse = await SendMutationAsync(client, HttpMethod.Put, $"/api/students/{first.Student.Id}", update);
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);
        Assert.Equal("Yıldız", updated!.LastName);
        Assert.Equal(StudentStatus.Frozen, updated.Status);

        var archive = await SendMutationAsync(client, HttpMethod.Delete, $"/api/students/{first.Student.Id}");
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/students/{first.Student.Id}")).StatusCode);
        var archivedList = await client.GetFromJsonAsync<PagedResponse<StudentListItemResponse>>("/api/students?includeArchived=true", JsonOptions);
        Assert.Contains(archivedList!.Items, x => x.Id == first.Student.Id && x.IsArchived);
    }

    [Fact]
    public async Task GuardianMutation_ValidatesStudentOwnership()
    {
        await factory.ResetAsync();
        using var client = CreateClient("Reception");
        var first = await CreateStudentAsync(client, "Deniz", "Acar", null, null);
        var second = await CreateStudentAsync(client, "Ece", "Acar", null, null);
        var guardianRequest = new GuardianUpsertRequest("Mert", "Acar", "Baba", "05330000000", "mert@example.com");
        var add = await SendMutationAsync(client, HttpMethod.Post, $"/api/students/{first.Student.Id}/guardians", guardianRequest);
        add.EnsureSuccessStatusCode();
        var guardian = await add.Content.ReadFromJsonAsync<GuardianResponse>(JsonOptions);

        var crossStudentUpdate = await SendMutationAsync(client, HttpMethod.Put, $"/api/students/{second.Student.Id}/guardians/{guardian!.Id}", guardianRequest with { FirstName = "Değiştirilemez" });
        Assert.Equal(HttpStatusCode.NotFound, crossStudentUpdate.StatusCode);
        var guardians = await client.GetFromJsonAsync<IReadOnlyCollection<GuardianResponse>>($"/api/students/{first.Student.Id}/guardians", JsonOptions);
        Assert.Equal("Mert", guardians!.Single().FirstName);
    }

    private HttpClient CreateClient(string role)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        return client;
    }

    private static async Task<CreateStudentResponse> CreateStudentAsync(HttpClient client, string firstName, string lastName, string? phone, string? email)
    {
        var request = new StudentUpsertRequest(firstName, lastName, phone, email, null, null, null, StudentStatus.Active, new DateOnly(2026, 1, 1));
        var response = await SendMutationAsync(client, HttpMethod.Post, "/api/students", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreateStudentResponse>(JsonOptions))!;
    }

    private static async Task<HttpResponseMessage> SendMutationAsync(HttpClient client, HttpMethod method, string path, object? body = null)
    {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-XSRF-TOKEN", csrf.GetProperty("token").GetString());
        if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
        return await client.SendAsync(request);
    }
}
