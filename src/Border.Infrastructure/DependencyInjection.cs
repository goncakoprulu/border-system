using Border.Application.Auditing;
using Border.Infrastructure.Auditing;
using Border.Infrastructure.Identity;
using Border.Infrastructure.Persistence;
using Border.Application.Students;
using Border.Infrastructure.Students;
using Border.Application.Classes;
using Border.Infrastructure.Classes;
using Border.Application.Operations;
using Border.Infrastructure.Operations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Net.Security;
using System.Security.Authentication;

namespace Border.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection yapılandırılmalıdır.");
        var requireSecureCookies = configuration.GetValue<bool>("Security:RequireSecureCookies");
        var forceTls12 = PostgreSqlTlsConfiguration.IsEnabled(configuration);

        services.AddSingleton<NpgsqlDataSource>(serviceProvider =>
        {
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            if (forceTls12)
            {
                dataSourceBuilder.UseSslClientAuthenticationOptionsCallback(PostgreSqlTlsConfiguration.Apply);
                serviceProvider.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("PostgreSqlTls")
                    .LogInformation("PostgreSQL bağlantısında TLS 1.2 zorlaması etkin.");
            }

            return dataSourceBuilder.Build();
        });
        services.AddDbContext<BorderDbContext>((serviceProvider, options) =>
            options.UseNpgsql(serviceProvider.GetRequiredService<NpgsqlDataSource>()));
        services.AddIdentity<AppUser, IdentityRole>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 10;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
        })
        .AddEntityFrameworkStores<BorderDbContext>()
        .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "border.session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = requireSecureCookies ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
            options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
        });

        services.AddHttpContextAccessor();
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<IStudentService, StudentService>();
        services.AddScoped<IClassService, ClassService>();
        services.AddScoped<IOperationsService, OperationsService>();
        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<DemoDataSeeder>();
        return services;
    }
}

public static class PostgreSqlTlsConfiguration
{
    public static bool IsEnabled(IConfiguration configuration) =>
        configuration.GetValue<bool>("Database:ForceTls12");

    public static void Apply(SslClientAuthenticationOptions options) =>
        options.EnabledSslProtocols = SslProtocols.Tls12;
}
