using System.Security.Claims;
using Border.Application.Auditing;
using Border.Application.Auth;
using Border.Application.Operations;
using Border.Domain.Entities;
using Border.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Border.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class OperationsController(IOperationsService operations, UserManager<AppUser> userManager, IAuditWriter auditWriter) : ControllerBase
{
    [HttpGet("dashboard/operations")]
    [Authorize(Policy = Policies.OperationsAccess)]
    public async Task<ActionResult<DashboardOperationsResponse>> DashboardOperations(CancellationToken ct) =>
        Ok(await operations.GetDashboardOperationsAsync(UserId(), InstructorOnly(), ct));

    [HttpGet("dashboard/analytics")]
    [Authorize(Policy = Policies.OperationsAccess)]
    public async Task<ActionResult<DashboardAnalyticsResponse>> DashboardAnalytics(CancellationToken ct) =>
        Ok(await operations.GetDashboardAnalyticsAsync(UserId(), InstructorOnly(), CanViewFinance(), ct));

    [HttpGet("schedule")]
    [Authorize(Policy = Policies.OperationsAccess)]
    public async Task<ActionResult<IReadOnlyCollection<ScheduleItemResponse>>> Schedule([FromQuery] Guid? roomId, [FromQuery] Guid? instructorId, [FromQuery] DayOfWeek? day, [FromQuery] Guid? classId, CancellationToken ct) => Ok(await operations.GetScheduleAsync(roomId, instructorId, day, classId, ct));

    [HttpGet("attendance/sessions")]
    [Authorize(Policy = Policies.OperationsAccess)]
    public async Task<ActionResult<IReadOnlyCollection<SessionListItemResponse>>> Sessions([FromQuery] DateOnly? date, [FromQuery] Guid? instructorId, [FromQuery] Guid? classId, [FromQuery] Guid? roomId, [FromQuery] Guid? studentId, CancellationToken ct) =>
        Ok(await operations.GetSessionsAsync(date ?? Today(), instructorId, classId, roomId, studentId, UserId(), InstructorOnly(), ct));

    [HttpGet("attendance/sessions/{id:guid}")]
    [Authorize(Policy = Policies.OperationsAccess)]
    public async Task<ActionResult<AttendanceDetailResponse>> Attendance(Guid id, CancellationToken ct) { var result = await operations.GetAttendanceAsync(id, UserId(), InstructorOnly(), ct); return result is null ? NotFound() : Ok(result); }

    [HttpPut("attendance/sessions/{id:guid}")]
    [Authorize(Policy = Policies.OperationsAccess)]
    public async Task<ActionResult<AttendanceDetailResponse>> SaveAttendance(Guid id, SaveAttendanceRequest request, CancellationToken ct)
    { try { var result = await operations.SaveAttendanceAsync(id, request, UserId()!, InstructorOnly(), ct); return result is null ? NotFound() : Ok(result); } catch (InvalidOperationException ex) { return Validation(ex.Message); } }

    [HttpGet("students/{studentId:guid}/attendance-history")]
    [Authorize(Policy = Policies.OperationsAccess)]
    public async Task<ActionResult<StudentAttendanceHistoryResponse>> StudentAttendanceHistory(Guid studentId, CancellationToken ct)
    { var result = await operations.GetStudentAttendanceHistoryAsync(studentId, ct); return result is null ? NotFound() : Ok(result); }

    [HttpGet("memberships")]
    [Authorize(Policy = Policies.FinanceAccess)]
    public async Task<ActionResult<IReadOnlyCollection<MembershipListItemResponse>>> Memberships([FromQuery] string? search, [FromQuery] MembershipStatus? status, CancellationToken ct) => Ok(await operations.GetMembershipsAsync(search, status, ct));

    [HttpPost("memberships")]
    [Authorize(Policy = Policies.FinanceAccess)]
    public async Task<ActionResult<MembershipListItemResponse>> CreateMembership(CreateMembershipRequest request, CancellationToken ct)
    { try { var result = await operations.CreateMembershipAsync(request, UserId()!, ct); return Created($"/api/memberships/{result.Id}", result); } catch (InvalidOperationException ex) { return Validation(ex.Message); } }

    [HttpPatch("memberships/{id:guid}/status")]
    [Authorize(Policy = Policies.FinanceAccess)]
    public async Task<ActionResult<MembershipListItemResponse>> ChangeMembershipStatus(Guid id, ChangeMembershipStatusRequest request, CancellationToken ct)
    { try { var result = await operations.ChangeMembershipStatusAsync(id, request, UserId()!, ct); return result is null ? NotFound() : Ok(result); } catch (InvalidOperationException ex) { return Validation(ex.Message); } }

    [HttpGet("membership-plans")]
    [Authorize(Policy = Policies.FinanceAccess)]
    public async Task<ActionResult<IReadOnlyCollection<MembershipPlanResponse>>> Plans([FromQuery] bool activeOnly = true, CancellationToken ct = default) => Ok(await operations.GetPlansAsync(activeOnly, ct));

    [HttpPost("membership-plans")]
    [Authorize(Policy = Policies.SettingsManage)]
    public async Task<ActionResult<MembershipPlanResponse>> CreatePlan(MembershipPlanRequest request, CancellationToken ct)
    { try { var result = await operations.CreatePlanAsync(request, ct); return Created($"/api/membership-plans/{result.Id}", result); } catch (InvalidOperationException ex) { return Validation(ex.Message); } }

    [HttpPut("membership-plans/{id:guid}")]
    [Authorize(Policy = Policies.SettingsManage)]
    public async Task<ActionResult<MembershipPlanResponse>> UpdatePlan(Guid id, MembershipPlanRequest request, CancellationToken ct)
    { try { var result = await operations.UpdatePlanAsync(id, request, ct); return result is null ? NotFound() : Ok(result); } catch (InvalidOperationException ex) { return Validation(ex.Message); } }

    [HttpGet("payments")]
    [Authorize(Policy = Policies.FinanceAccess)]
    public async Task<ActionResult<IReadOnlyCollection<PaymentListItemResponse>>> Payments([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? search, CancellationToken ct) => Ok(await operations.GetPaymentsAsync(from, to, search, ct));

    [HttpGet("students/{studentId:guid}/open-invoices")]
    [Authorize(Policy = Policies.FinanceAccess)]
    public async Task<ActionResult<IReadOnlyCollection<InvoiceOptionResponse>>> OpenInvoices(Guid studentId, CancellationToken ct) => Ok(await operations.GetOpenInvoicesAsync(studentId, ct));

    [HttpPost("payments")]
    [Authorize(Policy = Policies.FinanceAccess)]
    public async Task<ActionResult<PaymentListItemResponse>> CreatePayment(CreatePaymentRequest request, CancellationToken ct)
    { try { var result = await operations.CreatePaymentAsync(request, UserId()!, ct); return Created($"/api/payments/{result.Id}", result); } catch (InvalidOperationException ex) { return Validation(ex.Message); } }

    [HttpGet("balances")]
    [Authorize(Policy = Policies.FinanceAccess)]
    public async Task<ActionResult<BalancesResponse>> Balances([FromQuery] string? search, [FromQuery] bool overdueOnly = false, [FromQuery] bool openOnly = false, [FromQuery] bool includeSettled = false, CancellationToken ct = default) =>
        Ok(await operations.GetBalancesAsync(search, overdueOnly, openOnly, includeSettled, ct));

    [HttpGet("students/{studentId:guid}/finance-overview")]
    [Authorize(Policy = Policies.FinanceAccess)]
    public async Task<ActionResult<StudentFinanceOverviewResponse>> StudentFinanceOverview(Guid studentId, CancellationToken ct)
    { var result = await operations.GetStudentFinanceOverviewAsync(studentId, ct); return result is null ? NotFound() : Ok(result); }

    [HttpGet("reports")]
    [Authorize(Policy = Policies.ReportsAccess)]
    public async Task<ActionResult<ReportsResponse>> Reports(CancellationToken ct) => Ok(await operations.GetReportsAsync(ct));

    [HttpGet("search")]
    [Authorize(Policy = Policies.OperationsAccess)]
    public async Task<ActionResult<GlobalSearchResponse>> Search([FromQuery] string? q, CancellationToken ct) =>
        Ok(await operations.SearchAsync(q ?? string.Empty, UserId(), InstructorOnly(), ct));

    [HttpGet("instructors/{id:guid}")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Management)]
    public async Task<ActionResult<InstructorDetailResponse>> Instructor(Guid id, CancellationToken ct) { var result = await operations.GetInstructorAsync(id, ct); return result is null ? NotFound() : Ok(result); }

    [HttpGet("users")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<IReadOnlyCollection<UserResponse>>> Users(CancellationToken ct)
    { var users = userManager.Users.OrderBy(x => x.DisplayName).ToList(); var result = new List<UserResponse>(); foreach (var user in users) result.Add(new(user.Id, user.DisplayName, user.Email ?? "", (await userManager.GetRolesAsync(user)).ToArray(), user.IsActive)); return Ok(result); }

    [HttpPut("users/{id}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<UserResponse>> UpdateUser(string id, UpdateUserRequest request)
    {
        var user = await userManager.FindByIdAsync(id); if (user is null) return NotFound();
        var roles = request.Roles.Distinct().ToArray(); if (roles.Any(x => !Roles.All.Contains(x))) return Validation("Geçersiz rol seçildi."); if (string.IsNullOrWhiteSpace(request.DisplayName)) return Validation("Ad zorunludur.");
        var current = await userManager.GetRolesAsync(user); var old = new { user.DisplayName, user.IsActive, Roles = current };
        user.DisplayName = request.DisplayName.Trim(); user.IsActive = request.IsActive; var update = await userManager.UpdateAsync(user); if (!update.Succeeded) return Validation(string.Join(" ", update.Errors.Select(x => x.Description)));
        await userManager.RemoveFromRolesAsync(user, current.Except(roles)); await userManager.AddToRolesAsync(user, roles.Except(current));
        await auditWriter.WriteAsync("UserAccessUpdated", nameof(AppUser), user.Id, old, new { user.DisplayName, user.IsActive, Roles = roles });
        return Ok(new UserResponse(user.Id, user.DisplayName, user.Email ?? "", (await userManager.GetRolesAsync(user)).ToArray(), user.IsActive));
    }

    private string? UserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
    private bool InstructorOnly() => User.IsInRole(Roles.Instructor) && !User.IsInRole(Roles.Admin) && !User.IsInRole(Roles.Management) && !User.IsInRole(Roles.Reception);
    private bool CanViewFinance() => User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Management) || User.IsInRole(Roles.Reception);
    private BadRequestObjectResult Validation(string detail) => BadRequest(new ProblemDetails { Title = "Doğrulama hatası", Detail = detail, Status = 400 });
    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
}
