using Microsoft.AspNetCore.Mvc;
using SmartStaff.Application.Health;

namespace SmartStaff.API.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    private readonly IHealthService _healthService;

    public HealthController(IHealthService healthService)
    {
        _healthService = healthService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(HealthReport), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthReport), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthReport>> Get(CancellationToken cancellationToken)
    {
        var report = await _healthService.CheckAsync(cancellationToken);
        if (report.Status != HealthReport.HealthyStatus)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, report);
        }

        return Ok(report);
    }
}
