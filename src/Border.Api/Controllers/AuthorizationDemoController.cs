using Border.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Border.Api.Controllers;

[ApiController]
[Route("api/access")]
public sealed class AuthorizationDemoController : ControllerBase
{
    [HttpGet("authenticated")]
    [Authorize]
    public IActionResult Authenticated() => Ok(new { message = "Authenticated access granted." });

    [HttpGet("management")]
    [Authorize(Policy = Policies.ManagementOnly)]
    public IActionResult Management() => Ok(new { message = "Management access granted." });

    [HttpGet("instructor")]
    [Authorize(Policy = Policies.InstructorOnly)]
    public IActionResult Instructor() => Ok(new { message = "Instructor access granted." });

    [HttpGet("admin")]
    [Authorize(Policy = Policies.AdminOnly)]
    public IActionResult Admin() => Ok(new { message = "Admin access granted." });
}
