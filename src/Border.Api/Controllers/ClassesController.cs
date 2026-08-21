using System.Security.Claims;
using Border.Application.Auth;
using Border.Application.Classes;
using Border.Application.Students;
using Border.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Border.Api.Controllers;

[ApiController]
[Route("api/classes")]
[Authorize(Policy = Policies.ClassesAccess)]
public sealed class ClassesController(IClassService classService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<ClassListItemResponse>>> GetClasses(
        [FromQuery] string? search, [FromQuery] StudioClassStatus? status, [FromQuery] Guid? instructorId,
        [FromQuery] Guid? roomId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string sortBy = "name", [FromQuery] string sortDirection = "asc",
        [FromQuery] bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        if (includeArchived && !CanArchive()) return Forbid();
        var result = await classService.GetClassesAsync(new(search, status, instructorId, roomId, page, pageSize, sortBy, sortDirection, includeArchived), Scope(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ClassDetailResponse>> GetClass(Guid id, [FromQuery] bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        if (includeArchived && !CanArchive()) return Forbid();
        var result = await classService.GetClassAsync(id, Scope(), includeArchived, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = Policies.ClassesManage)]
    public async Task<ActionResult<ClassDetailResponse>> CreateClass(StudioClassUpsertRequest request, CancellationToken cancellationToken)
    {
        var errors = ClassValidation.Validate(request);
        if (errors.Count > 0) return ValidationError(errors);
        var result = await classService.CreateClassAsync(request, cancellationToken);
        return result.IsConflict ? ConflictError(result.Error!) : CreatedAtAction(nameof(GetClass), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.ClassesManage)]
    public async Task<ActionResult<ClassDetailResponse>> UpdateClass(Guid id, StudioClassUpsertRequest request, CancellationToken cancellationToken)
    {
        var errors = ClassValidation.Validate(request);
        if (errors.Count > 0) return ValidationError(errors);
        return Operation(await classService.UpdateClassAsync(id, request, cancellationToken));
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = Policies.ClassesManage)]
    public async Task<ActionResult<ClassDetailResponse>> ChangeStatus(Guid id, ChangeClassStatusRequest request, CancellationToken cancellationToken) =>
        Operation(await classService.ChangeStatusAsync(id, request.Status, cancellationToken));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Management)]
    public async Task<IActionResult> ArchiveClass(Guid id, CancellationToken cancellationToken) =>
        await classService.ArchiveClassAsync(id, cancellationToken) ? NoContent() : NotFound();

    [HttpGet("{id:guid}/schedules")]
    public async Task<ActionResult<IReadOnlyCollection<ClassScheduleResponse>>> GetSchedules(Guid id, CancellationToken cancellationToken)
    {
        var result = await classService.GetClassAsync(id, Scope(), false, cancellationToken);
        return result is null ? NotFound() : Ok(result.Schedules);
    }

    [HttpPost("{id:guid}/schedules")]
    [Authorize(Policy = Policies.ClassesManage)]
    public async Task<ActionResult<ClassScheduleResponse>> AddSchedule(Guid id, ClassScheduleRequest request, CancellationToken cancellationToken)
    {
        var errors = ClassValidation.Validate(request);
        if (errors.Count > 0) return ValidationError(errors);
        var result = await classService.AddScheduleAsync(id, request, cancellationToken);
        if (result.Value is not null) return CreatedAtAction(nameof(GetSchedules), new { id }, result.Value);
        return result.IsConflict ? ConflictError(result.Error!) : NotFound();
    }

    [HttpPut("{id:guid}/schedules/{scheduleId:guid}")]
    [Authorize(Policy = Policies.ClassesManage)]
    public async Task<ActionResult<ClassScheduleResponse>> UpdateSchedule(Guid id, Guid scheduleId, ClassScheduleRequest request, CancellationToken cancellationToken)
    {
        var errors = ClassValidation.Validate(request);
        if (errors.Count > 0) return ValidationError(errors);
        var result = await classService.UpdateScheduleAsync(id, scheduleId, request, cancellationToken);
        if (result.Value is not null) return Ok(result.Value);
        return result.IsConflict ? ConflictError(result.Error!) : NotFound();
    }

    [HttpDelete("{id:guid}/schedules/{scheduleId:guid}")]
    [Authorize(Policy = Policies.ClassesManage)]
    public async Task<IActionResult> DeleteSchedule(Guid id, Guid scheduleId, CancellationToken cancellationToken)
    {
        var result = await classService.DeleteScheduleAsync(id, scheduleId, cancellationToken);
        return result == true ? NoContent() : NotFound();
    }

    [HttpGet("{id:guid}/enrollments")]
    public async Task<ActionResult<IReadOnlyCollection<ClassEnrollmentResponse>>> GetEnrollments(Guid id, CancellationToken cancellationToken)
    {
        var result = await classService.GetClassAsync(id, Scope(), false, cancellationToken);
        return result is null ? NotFound() : Ok(result.Enrollments);
    }

    [HttpPost("{id:guid}/enrollments")]
    [Authorize(Policy = Policies.ClassesManage)]
    public async Task<ActionResult<ClassEnrollmentResponse>> Enroll(Guid id, CreateEnrollmentRequest request, CancellationToken cancellationToken)
    {
        if (request.StudentId == Guid.Empty || request.StartDate == default)
            return ValidationError(new() { ["enrollment"] = ["Öğrenci ve başlangıç tarihi zorunludur."] });
        var result = await classService.EnrollStudentAsync(id, request, cancellationToken);
        if (result.Value is not null) return CreatedAtAction(nameof(GetEnrollments), new { id }, result.Value);
        return result.IsConflict ? ConflictError(result.Error!) : NotFound();
    }

    [HttpPatch("{id:guid}/enrollments/{enrollmentId:guid}/end")]
    [Authorize(Policy = Policies.ClassesManage)]
    public async Task<ActionResult<ClassEnrollmentResponse>> EndEnrollment(Guid id, Guid enrollmentId, EndEnrollmentRequest request, CancellationToken cancellationToken) =>
        EnrollmentOperation(await classService.EndEnrollmentAsync(id, enrollmentId, request, cancellationToken));

    private ClassAccessScope Scope() => new(IsInstructorOnly(), User.FindFirstValue(ClaimTypes.NameIdentifier));
    private bool IsInstructorOnly() => User.IsInRole(Roles.Instructor) && !User.IsInRole(Roles.Admin) && !User.IsInRole(Roles.Management) && !User.IsInRole(Roles.Reception);
    private bool CanArchive() => User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Management);

    private ActionResult<ClassDetailResponse> Operation(ClassOperationResult<ClassDetailResponse> result) =>
        result.Value is not null ? Ok(result.Value) : result.IsConflict ? ConflictError(result.Error!) : NotFound();

    private ActionResult<ClassEnrollmentResponse> EnrollmentOperation(ClassOperationResult<ClassEnrollmentResponse> result) =>
        result.Value is not null ? Ok(result.Value) : result.IsConflict ? ConflictError(result.Error!) : NotFound();

    private ObjectResult ConflictError(string detail) =>
        Conflict(new ProblemDetails { Title = "İşlem çakışması", Detail = detail, Status = StatusCodes.Status409Conflict });

    private BadRequestObjectResult ValidationError(Dictionary<string, string[]> errors) =>
        BadRequest(new ValidationProblemDetails(errors) { Title = "Doğrulama hatası", Detail = "Lütfen işaretlenen alanları kontrol edin." });
}
