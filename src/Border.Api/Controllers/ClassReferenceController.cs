using Border.Application.Auth;
using Border.Application.Classes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Border.Api.Controllers;

[ApiController]
[Authorize(Policy = Policies.ClassesManage)]
public sealed class ClassReferenceController(IClassService classService) : ControllerBase
{
    [HttpGet("api/instructors/options")]
    public async Task<ActionResult<IReadOnlyCollection<InstructorOptionResponse>>> GetInstructors(CancellationToken cancellationToken) =>
        Ok(await classService.GetInstructorOptionsAsync(cancellationToken));

    [HttpGet("api/instructors")]
    public async Task<ActionResult<IReadOnlyCollection<InstructorResponse>>> GetInstructorRecords(CancellationToken cancellationToken) =>
        Ok(await classService.GetInstructorsAsync(cancellationToken));

    [HttpGet("api/instructors/login-options")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Management)]
    public async Task<ActionResult<IReadOnlyCollection<InstructorLoginOptionResponse>>> GetInstructorLoginOptions(CancellationToken cancellationToken) =>
        Ok(await classService.GetInstructorLoginOptionsAsync(cancellationToken));

    [HttpPost("api/instructors")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Management)]
    public async Task<ActionResult<InstructorResponse>> CreateInstructor(InstructorUpsertRequest request, CancellationToken cancellationToken)
    {
        var errors = ClassValidation.Validate(request);
        if (errors.Count > 0) return ValidationError(errors);
        var result = await classService.CreateInstructorAsync(request, cancellationToken);
        return result.IsConflict ? Conflict(new ProblemDetails { Title = "İşlem çakışması", Detail = result.Error, Status = 409 }) : Created($"/api/instructors/{result.Value!.Id}", result.Value);
    }

    [HttpPut("api/instructors/{id:guid}")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Management)]
    public async Task<ActionResult<InstructorResponse>> UpdateInstructor(Guid id, InstructorUpsertRequest request, CancellationToken cancellationToken)
    {
        var errors = ClassValidation.Validate(request);
        if (errors.Count > 0) return ValidationError(errors);
        var result = await classService.UpdateInstructorAsync(id, request, cancellationToken);
        if (result.Value is not null) return Ok(result.Value);
        return result.IsConflict ? Conflict(new ProblemDetails { Title = "İşlem çakışması", Detail = result.Error, Status = 409 }) : NotFound();
    }

    [HttpGet("api/rooms")]
    public async Task<ActionResult<IReadOnlyCollection<StudioRoomResponse>>> GetRooms([FromQuery] bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        if (includeArchived && !CanArchive()) return Forbid();
        return Ok(await classService.GetRoomsAsync(includeArchived, cancellationToken));
    }

    [HttpPost("api/rooms")]
    public async Task<ActionResult<StudioRoomResponse>> CreateRoom(StudioRoomUpsertRequest request, CancellationToken cancellationToken)
    {
        var errors = ClassValidation.Validate(request);
        if (errors.Count > 0) return ValidationError(errors);
        var room = await classService.CreateRoomAsync(request, cancellationToken);
        return Created($"/api/rooms/{room.Id}", room);
    }

    [HttpPut("api/rooms/{id:guid}")]
    public async Task<ActionResult<StudioRoomResponse>> UpdateRoom(Guid id, StudioRoomUpsertRequest request, CancellationToken cancellationToken)
    {
        var errors = ClassValidation.Validate(request);
        if (errors.Count > 0) return ValidationError(errors);
        var room = await classService.UpdateRoomAsync(id, request, cancellationToken);
        return room is null ? NotFound() : Ok(room);
    }

    [HttpDelete("api/rooms/{id:guid}")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Management)]
    public async Task<IActionResult> ArchiveRoom(Guid id, CancellationToken cancellationToken) =>
        await classService.ArchiveRoomAsync(id, cancellationToken) ? NoContent() : NotFound();

    private bool CanArchive() => User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Management);
    private BadRequestObjectResult ValidationError(Dictionary<string, string[]> errors) =>
        BadRequest(new ValidationProblemDetails(errors) { Title = "Doğrulama hatası", Detail = "Lütfen işaretlenen alanları kontrol edin." });
}
