using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStaff.API.Authentication;
using SmartStaff.Application.Analytics;

namespace SmartStaff.API.Controllers;

[ApiController]
[Authorize]
[Route("api/analytics")]
public sealed class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analytics;

    public AnalyticsController(IAnalyticsService analytics)
    {
        _analytics = analytics;
    }

    [HttpGet("overview")]
    public Task<IActionResult> Overview([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] Guid? facilityId, [FromQuery] string? recruiterId, CancellationToken cancellationToken) =>
        Send(query => _analytics.OverviewAsync(UserId(), query, cancellationToken), from, to, facilityId, recruiterId);

    [HttpGet("recruitment")]
    public Task<IActionResult> Recruitment([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] Guid? facilityId, [FromQuery] string? recruiterId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default) =>
        Send(query => _analytics.RecruitmentAsync(UserId(), query, cancellationToken), from, to, facilityId, recruiterId, page, pageSize);

    [HttpGet("scheduling")]
    public Task<IActionResult> Scheduling([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] Guid? facilityId, [FromQuery] string? recruiterId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default) =>
        Send(query => _analytics.SchedulingAsync(UserId(), query, cancellationToken), from, to, facilityId, recruiterId, page, pageSize);

    [HttpGet("attendance")]
    public Task<IActionResult> Attendance([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] Guid? facilityId, [FromQuery] string? recruiterId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default) =>
        Send(query => _analytics.AttendanceAsync(UserId(), query, cancellationToken), from, to, facilityId, recruiterId, page, pageSize);

    [HttpGet("timesheets")]
    public Task<IActionResult> Timesheets([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] Guid? facilityId, [FromQuery] string? recruiterId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default) =>
        Send(query => _analytics.TimesheetsAsync(UserId(), query, cancellationToken), from, to, facilityId, recruiterId, page, pageSize);

    [HttpGet("credentials")]
    public Task<IActionResult> Credentials([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] Guid? facilityId, [FromQuery] string? recruiterId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default) =>
        Send(query => _analytics.CredentialsAsync(UserId(), query, cancellationToken), from, to, facilityId, recruiterId, page, pageSize);

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string report, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] Guid? facilityId, [FromQuery] string? recruiterId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var result = await _analytics.ExportAsync(UserId(), report, new AnalyticsQuery(from, to, facilityId, recruiterId, page, pageSize), cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return this.FromFailure(result);
        }

        return File(result.Value.Content, "text/csv", result.Value.FileName);
    }

    private async Task<IActionResult> Send<T>(Func<AnalyticsQuery, Task<Application.Common.OperationResult<T>>> action, DateOnly? from, DateOnly? to, Guid? facilityId, string? recruiterId, int page = 1, int pageSize = 10)
    {
        var result = await action(new AnalyticsQuery(from, to, facilityId, recruiterId, page, pageSize));
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    private string UserId() => User.CurrentUserId() ?? "";
}
