using Border.Application.Auditing;
using Border.Application.Auth;
using Border.Infrastructure.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Border.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    SignInManager<AppUser> signInManager,
    UserManager<AppUser> userManager,
    IAuditWriter auditWriter,
    IAntiforgery antiforgery) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<CurrentUserResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(HttpContext);
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive)
            return Unauthorized(new ProblemDetails { Title = "Giriş başarısız", Detail = "E-posta adresi veya parola hatalı." });

        var result = await signInManager.PasswordSignInAsync(user, request.Password, request.RememberMe, lockoutOnFailure: true);
        if (!result.Succeeded)
            return Unauthorized(new ProblemDetails { Title = "Giriş başarısız", Detail = "E-posta adresi veya parola hatalı." });

        await auditWriter.WriteAsync("Login", nameof(AppUser), user.Id, null, new { Success = true }, cancellationToken);
        return Ok(await MapUserAsync(user));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(HttpContext);
        var userId = userManager.GetUserId(User) ?? "unknown";
        await signInManager.SignOutAsync();
        await auditWriter.WriteAsync("Logout", nameof(AppUser), userId, null, null, cancellationToken);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<CurrentUserResponse>> Me()
    {
        var user = await userManager.GetUserAsync(User);
        return user is null ? Unauthorized() : Ok(await MapUserAsync(user));
    }

    private async Task<CurrentUserResponse> MapUserAsync(AppUser user) =>
        new(user.Id, user.Email!, user.DisplayName, (await userManager.GetRolesAsync(user)).ToArray());
}
