using Border.Application.Auth;
using Border.Infrastructure;
using Border.Infrastructure.Persistence;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using System.Net;

var builder = WebApplication.CreateBuilder(args);
var isRender = builder.Configuration.GetValue<bool>("RENDER");
var reverseProxyEnabled = builder.Configuration.GetValue<bool>("ReverseProxy:Enabled") || isRender;
var requireSecureCookies = builder.Configuration.GetValue<bool>("Security:RequireSecureCookies");
var useHttpsRedirection = builder.Configuration.GetValue("Security:UseHttpsRedirection", true);
var useHsts = builder.Configuration.GetValue<bool>("Security:UseHsts");

var configuredKeyPath = builder.Configuration["DataProtection:KeyPath"];
if (builder.Environment.IsProduction() || !string.IsNullOrWhiteSpace(configuredKeyPath))
{
    var keyPath = string.IsNullOrWhiteSpace(configuredKeyPath)
        ? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys")
        : Path.IsPathRooted(configuredKeyPath)
            ? configuredKeyPath
            : Path.Combine(builder.Environment.ContentRootPath, configuredKeyPath);
    keyPath = Path.GetFullPath(keyPath);
    try
    {
        Directory.CreateDirectory(keyPath);
    }
    catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
    {
        throw new InvalidOperationException($"Data Protection anahtar klasörü oluşturulamadı: {keyPath}", exception);
    }

    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keyPath))
        .SetApplicationName("BORDER.Panel");
}

if (reverseProxyEnabled)
{
    var knownProxyAddresses = builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
    if (!isRender && knownProxyAddresses.Length == 0)
        throw new InvalidOperationException("ReverseProxy:KnownProxies en az bir güvenilir proxy IP adresi içermelidir.");

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardLimit = 1;
        options.ForwardedHeaders = isRender
            ? ForwardedHeaders.XForwardedProto
            : ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        if (isRender)
        {
            // Render terminates TLS at its edge and forwards HTTP from dynamic proxy IPs.
            // Trust one platform hop for the original scheme only; never accept a forwarded client IP here.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            return;
        }

        foreach (var address in knownProxyAddresses)
        {
            if (!IPAddress.TryParse(address, out var proxyAddress))
                throw new InvalidOperationException($"Geçersiz ReverseProxy:KnownProxies adresi: {address}");

            options.KnownProxies.Add(proxyAddress);
        }
    });
}

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<Border.Api.Security.ValidateAntiforgeryFilter>();
builder.Services.AddControllers(options => options.Filters.Add<Border.Api.Security.ValidateAntiforgeryFilter>())
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new Border.Api.Serialization.DayOfWeekJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddProblemDetails();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
    options.Cookie.Name = "border.xsrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = requireSecureCookies ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
});
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
{
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    if (allowedOrigins.Length > 0)
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.ManagementOnly, policy => policy.RequireRole(Roles.Management));
    options.AddPolicy(Policies.InstructorOnly, policy => policy.RequireRole(Roles.Instructor));
    options.AddPolicy(Policies.AdminOnly, policy => policy.RequireRole(Roles.Admin));
    options.AddPolicy(Policies.StudentsAccess, policy => policy.RequireRole(Roles.Admin, Roles.Management, Roles.Reception));
    options.AddPolicy(Policies.StudentsArchive, policy => policy.RequireRole(Roles.Admin, Roles.Management));
    options.AddPolicy(Policies.ClassesAccess, policy => policy.RequireRole(Roles.Admin, Roles.Management, Roles.Reception, Roles.Instructor));
    options.AddPolicy(Policies.ClassesManage, policy => policy.RequireRole(Roles.Admin, Roles.Management, Roles.Reception));
    options.AddPolicy(Policies.OperationsAccess, policy => policy.RequireRole(Roles.Admin, Roles.Management, Roles.Reception, Roles.Instructor));
    options.AddPolicy(Policies.FinanceAccess, policy => policy.RequireRole(Roles.Admin, Roles.Management, Roles.Reception));
    options.AddPolicy(Policies.ReportsAccess, policy => policy.RequireRole(Roles.Admin, Roles.Management));
    options.AddPolicy(Policies.SettingsManage, policy => policy.RequireRole(Roles.Admin));
});
builder.Services.AddHealthChecks().AddDbContextCheck<BorderDbContext>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "BORDER API", Version = "v1" });
    options.AddSecurityDefinition("cookieAuth", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Cookie,
        Name = "border.session",
        Description = "HttpOnly oturum çerezi login işlemi tarafından oluşturulur."
    });
});

var app = builder.Build();

app.UseExceptionHandler();
if (reverseProxyEnabled)
    app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (app.Environment.IsProduction() && useHttpsRedirection && useHsts)
    app.UseHsts();
if (useHttpsRedirection)
    app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        var requestPath = context.Context.Request.Path;
        if (requestPath.StartsWithSegments("/_next/static"))
            context.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        else if (string.Equals(Path.GetExtension(requestPath.Value), ".html", StringComparison.OrdinalIgnoreCase))
            context.Context.Response.Headers.CacheControl = "no-cache";
    }
});
app.UseRouting();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapGet("/api/auth/csrf", (HttpContext context, IAntiforgery antiforgery) =>
{
    var tokens = antiforgery.GetAndStoreTokens(context);
    return Results.Ok(new { token = tokens.RequestToken });
}).AllowAnonymous();
app.MapControllers();
app.MapFallback(async context =>
{
    if (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/health"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    context.Response.StatusCode = StatusCodes.Status404NotFound;
    context.Response.ContentType = "text/html; charset=utf-8";
    context.Response.Headers.CacheControl = "no-cache";
    var webRootPath = app.Environment.WebRootPath;
    if (!string.IsNullOrWhiteSpace(webRootPath))
    {
        var notFoundPath = Path.Combine(webRootPath, "404.html");
        if (File.Exists(notFoundPath)) await context.Response.SendFileAsync(notFoundPath);
    }
});

if (!app.Environment.IsEnvironment("Testing"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseStartup");
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<BorderDbContext>();
        if (builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
        {
            logger.LogInformation("Bekleyen PostgreSQL migration'ları uygulanıyor.");
            await db.Database.MigrateAsync();
        }
        else
        {
            logger.LogInformation("Startup migration devre dışı; veritabanı şeması deployment öncesinde ayrıca uygulanmalıdır.");
        }

        await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
        if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("SEED_DEMO_DATA"))
            await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync();
    }
    catch (Exception exception)
    {
        logger.LogCritical(exception, "PostgreSQL başlatma işlemi tamamlanamadı. Connection string değeri loglanmadı.");
        throw;
    }
}

app.Run();

public partial class Program;
