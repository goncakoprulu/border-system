using Border.Application.Auth;
using Border.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Border.Infrastructure.Persistence;

public sealed class DatabaseSeeder(
    RoleManager<IdentityRole> roleManager,
    UserManager<AppUser> userManager,
    IConfiguration configuration,
    ILogger<DatabaseSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var role in Roles.All)
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));

        var email = configuration["BOOTSTRAP_ADMIN_EMAIL"];
        var password = configuration["BOOTSTRAP_ADMIN_PASSWORD"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new AppUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = "BORDER Admin" };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
                throw new InvalidOperationException($"Bootstrap admin oluşturulamadı: {string.Join(", ", result.Errors.Select(x => x.Description))}");
            logger.LogInformation("Yerel bootstrap admin kullanıcısı oluşturuldu: {Email}", email);
        }

        if (!await userManager.IsInRoleAsync(user, Roles.Admin)) await userManager.AddToRoleAsync(user, Roles.Admin);
        if (!await userManager.IsInRoleAsync(user, Roles.Management)) await userManager.AddToRoleAsync(user, Roles.Management);
    }
}
