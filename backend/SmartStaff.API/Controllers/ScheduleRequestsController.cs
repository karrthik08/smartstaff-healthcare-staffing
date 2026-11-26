using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStaff.API.Authentication;
using SmartStaff.Application.Scheduling;

namespace SmartStaff.API.Controllers;

[ApiController]
[Authorize]
public sealed class ScheduleRequestsController : ControllerBase
{
    private readonly ISchedulingService _scheduling;
    private readonly IValidator<AvailabilityRequest> _availabilityValidator;
    private readonly IValidator<ShiftChangeRequestBody> _changeValidator;
    private readonly IValidator<ReviewShiftChangeRequest> _reviewValidator;

    public ScheduleRequestsController(
        ISchedulingService scheduling,
        IValidator<AvailabilityRequest> availabilityValidator,
        IValidator<ShiftChangeRequestBody> changeValidator,
        IValidator<ReviewShiftChangeRequest> reviewValidator)
    {
        _scheduling = scheduling;
        _availabilityValidator = availabilityValidator;
        _changeValidator = changeValidator;
        _reviewValidator = reviewValidator;
    }

    [HttpPost("api/availability")]
    public async Task<IActionResult> AddAvailability(AvailabilityRequest request, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(_availabilityValidator, request, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await _scheduling.AddAvailabilityAsync(request, UserId(), HttpContext.ClientIp(), cancellationToken);
        return result.Succeeded ? StatusCode(StatusCodes.Status201Created, result.Value) : this.FromFailure(result);
    }

    [HttpGet("api/availability/me")]
    public async Task<IActionResult> Availability(CancellationToken cancellationToken)
    {
        var result = await _scheduling.ListAvailabilityAsync(UserId(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpPost("api/shift-changes")]
    public async Task<IActionResult> RequestChange(ShiftChangeRequestBody request, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(_changeValidator, request, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await _scheduling.RequestChangeAsync(request, UserId(), HttpContext.ClientIp(), cancellationToken);
        return result.Succeeded ? StatusCode(StatusCodes.Status201Created, result.Value) : this.FromFailure(result);
    }

    [HttpGet("api/shift-changes")]
    public async Task<IActionResult> Changes(CancellationToken cancellationToken)
    {
        var result = await _scheduling.ListChangesAsync(UserId(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpPatch("api/shift-changes/{id:guid}")]
    public async Task<IActionResult> Review(Guid id, ReviewShiftChangeRequest request, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(_reviewValidator, request, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await _scheduling.ReviewChangeAsync(id, request, UserId(), HttpContext.ClientIp(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    private string UserId() => User.CurrentUserId() ?? "";

    private static async Task<IActionResult?> ValidateAsync<T>(IValidator<T> validator, T request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        return validation.IsValid ? null : new BadRequestObjectResult(new ValidationProblemDetails(validation.ToDictionary()));
    }
}
