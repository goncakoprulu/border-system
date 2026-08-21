using Border.Application.Auth;
using Border.Application.Students;
using Border.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Border.Api.Controllers;

[ApiController]
[Route("api/students")]
[Authorize(Policy = Policies.StudentsAccess)]
public sealed class StudentsController(IStudentService studentService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<StudentListItemResponse>>> GetStudents(
        [FromQuery] string? search,
        [FromQuery] StudentStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string sortBy = "name",
        [FromQuery] string sortDirection = "asc",
        [FromQuery] bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        if (includeArchived && !CanArchive()) return Forbid();
        return Ok(await studentService.GetStudentsAsync(new(search, status, page, pageSize, sortBy, sortDirection, includeArchived), cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StudentDetailResponse>> GetStudent(Guid id, [FromQuery] bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        if (includeArchived && !CanArchive()) return Forbid();
        var student = await studentService.GetStudentAsync(id, includeArchived, cancellationToken);
        return student is null ? NotFound() : Ok(student);
    }

    [HttpPost]
    public async Task<ActionResult<CreateStudentResponse>> CreateStudent(StudentUpsertRequest request, CancellationToken cancellationToken)
    {
        var errors = StudentValidation.Validate(request);
        if (errors.Count > 0) return ValidationError(errors);
        var result = await studentService.CreateStudentAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetStudent), new { id = result.Student.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StudentDetailResponse>> UpdateStudent(Guid id, StudentUpsertRequest request, CancellationToken cancellationToken)
    {
        var errors = StudentValidation.Validate(request);
        if (errors.Count > 0) return ValidationError(errors);
        var student = await studentService.UpdateStudentAsync(id, request, cancellationToken);
        return student is null ? NotFound() : Ok(student);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<StudentDetailResponse>> ChangeStatus(Guid id, ChangeStudentStatusRequest request, CancellationToken cancellationToken)
    {
        var student = await studentService.ChangeStatusAsync(id, request.Status, cancellationToken);
        return student is null ? NotFound() : Ok(student);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.StudentsArchive)]
    public async Task<IActionResult> ArchiveStudent(Guid id, CancellationToken cancellationToken) =>
        await studentService.ArchiveStudentAsync(id, cancellationToken) ? NoContent() : NotFound();

    [HttpGet("{studentId:guid}/guardians")]
    public async Task<ActionResult<IReadOnlyCollection<GuardianResponse>>> GetGuardians(Guid studentId, CancellationToken cancellationToken)
    {
        var guardians = await studentService.GetGuardiansAsync(studentId, cancellationToken);
        return guardians is null ? NotFound() : Ok(guardians);
    }

    [HttpPost("{studentId:guid}/guardians")]
    public async Task<ActionResult<GuardianResponse>> AddGuardian(Guid studentId, GuardianUpsertRequest request, CancellationToken cancellationToken)
    {
        var errors = StudentValidation.Validate(request);
        if (errors.Count > 0) return ValidationError(errors);
        var guardian = await studentService.AddGuardianAsync(studentId, request, cancellationToken);
        return guardian is null ? NotFound() : CreatedAtAction(nameof(GetGuardians), new { studentId }, guardian);
    }

    [HttpPut("{studentId:guid}/guardians/{guardianId:guid}")]
    public async Task<ActionResult<GuardianResponse>> UpdateGuardian(Guid studentId, Guid guardianId, GuardianUpsertRequest request, CancellationToken cancellationToken)
    {
        var errors = StudentValidation.Validate(request);
        if (errors.Count > 0) return ValidationError(errors);
        var guardian = await studentService.UpdateGuardianAsync(studentId, guardianId, request, cancellationToken);
        return guardian is null ? NotFound() : Ok(guardian);
    }

    [HttpDelete("{studentId:guid}/guardians/{guardianId:guid}")]
    public async Task<IActionResult> DeleteGuardian(Guid studentId, Guid guardianId, CancellationToken cancellationToken)
    {
        var deleted = await studentService.DeleteGuardianAsync(studentId, guardianId, cancellationToken);
        return deleted == true ? NoContent() : NotFound();
    }

    private bool CanArchive() => User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Management);
    private BadRequestObjectResult ValidationError(Dictionary<string, string[]> errors) =>
        BadRequest(new ValidationProblemDetails(errors) { Title = "Doğrulama hatası", Detail = "Lütfen işaretlenen alanları kontrol edin." });
}
