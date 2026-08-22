using Border.Application.Auth;
using Border.Application.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Border.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Policy = Policies.ReportsAccess)]
public sealed class ReportsController(IReportingService reporting) : ControllerBase
{
    [HttpGet("summary")]
    public Task<ActionResult<ReportingSummaryResponse>> Summary(DateOnly? from, DateOnly? to, Guid? instructorId, Guid? classId, Guid? roomId, CancellationToken ct) => Run(from, to, instructorId, classId, roomId, reporting.GetSummaryAsync, ct);

    [HttpGet("finance")]
    public Task<ActionResult<ReportingFinanceResponse>> Finance(DateOnly? from, DateOnly? to, Guid? instructorId, Guid? classId, Guid? roomId, CancellationToken ct) => Run(from, to, instructorId, classId, roomId, reporting.GetFinanceAsync, ct);

    [HttpGet("engagement")]
    public Task<ActionResult<ReportingEngagementResponse>> Engagement(DateOnly? from, DateOnly? to, Guid? instructorId, Guid? classId, Guid? roomId, CancellationToken ct) => Run(from, to, instructorId, classId, roomId, reporting.GetEngagementAsync, ct);

    [HttpGet("capacity")]
    public Task<ActionResult<ReportingCapacityResponse>> Capacity(DateOnly? from, DateOnly? to, Guid? instructorId, Guid? classId, Guid? roomId, CancellationToken ct) => Run(from, to, instructorId, classId, roomId, reporting.GetCapacityAsync, ct);

    private async Task<ActionResult<T>> Run<T>(DateOnly? from, DateOnly? to, Guid? instructorId, Guid? classId, Guid? roomId, Func<ReportFilter, CancellationToken, Task<T>> query, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3)); var effectiveFrom = from ?? new DateOnly(today.Year, today.Month, 1); var effectiveTo = to ?? today;
        if (effectiveFrom > effectiveTo) return BadRequest(new ProblemDetails { Title = "Doğrulama hatası", Detail = "Başlangıç tarihi bitiş tarihinden sonra olamaz.", Status = 400 });
        if (effectiveTo.DayNumber - effectiveFrom.DayNumber > 731) return BadRequest(new ProblemDetails { Title = "Doğrulama hatası", Detail = "Rapor tarih aralığı en fazla iki yıl olabilir.", Status = 400 });
        return Ok(await query(new(effectiveFrom, effectiveTo, instructorId, classId, roomId), ct));
    }
}
