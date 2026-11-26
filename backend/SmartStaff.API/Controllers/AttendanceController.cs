using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStaff.API.Authentication;
using SmartStaff.Application.Timekeeping;

namespace SmartStaff.API.Controllers;

[ApiController]
[Route("api/attendance")]
[Authorize]
public sealed class AttendanceController : ControllerBase
{
    private readonly ITimekeepingService _timekeeping;

    public AttendanceController(ITimekeepingService timekeeping)
    {
        _timekeeping = timekeeping;
    }

    [HttpPost("{assignmentId:guid}/clock-in")]
    public async Task<IActionResult> ClockIn(Guid assignmentId, ClockRequest request, CancellationToken cancellationToken)
    {
        var result = await _timekeeping.ClockInAsync(assignmentId, request, UserId(), HttpContext.ClientIp(), cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return this.FromFailure(result);
        }

        return result.Value.Created ? StatusCode(StatusCodes.Status201Created, result.Value) : Ok(result.Value);
    }

    [HttpPost("{assignmentId:guid}/clock-out")]
    public async Task<IActionResult> ClockOut(Guid assignmentId, ClockRequest request, CancellationToken cancellationToken)
    {
        var result = await _timekeeping.ClockOutAsync(assignmentId, request, UserId(), HttpContext.ClientIp(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await _timekeeping.ListAttendanceAsync(UserId(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    private string UserId() => User.CurrentUserId() ?? "";
}
