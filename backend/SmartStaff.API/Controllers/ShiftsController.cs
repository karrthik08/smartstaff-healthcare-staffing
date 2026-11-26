using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStaff.API.Authentication;
using SmartStaff.Application.Scheduling;

namespace SmartStaff.API.Controllers;

[ApiController]
[Route("api/shifts")]
[Authorize]
public sealed class ShiftsController : ControllerBase
{
    private readonly ISchedulingService _scheduling;
    private readonly IValidator<ShiftRequest> _shiftValidator;
    private readonly IValidator<AssignShiftRequest> _assignValidator;

    public ShiftsController(ISchedulingService scheduling, IValidator<ShiftRequest> shiftValidator, IValidator<AssignShiftRequest> assignValidator)
    {
        _scheduling = scheduling;
        _shiftValidator = shiftValidator;
        _assignValidator = assignValidator;
    }

    [HttpPost]
    public async Task<IActionResult> Create(ShiftRequest request, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(_shiftValidator, request, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await _scheduling.CreateAsync(request, UserId(), HttpContext.ClientIp(), cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return this.FromFailure(result);
        }

        return CreatedAtAction(nameof(Get), new { id = result.Value.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, ShiftRequest request, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(_shiftValidator, request, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await _scheduling.UpdateAsync(id, request, UserId(), HttpContext.ClientIp(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpPatch("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken)
    {
        var result = await _scheduling.PublishAsync(id, UserId(), HttpContext.ClientIp(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpPatch("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _scheduling.CancelAsync(id, UserId(), HttpContext.ClientIp(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ShiftListQuery query, CancellationToken cancellationToken)
    {
        var result = await _scheduling.ListAsync(query, UserId(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await _scheduling.GetAsync(id, UserId(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpPost("{id:guid}/assignments")]
    public async Task<IActionResult> Assign(Guid id, AssignShiftRequest request, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(_assignValidator, request, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await _scheduling.AssignAsync(id, request, UserId(), HttpContext.ClientIp(), cancellationToken);
        return result.Succeeded ? StatusCode(StatusCodes.Status201Created, result.Value) : this.FromFailure(result);
    }

    [HttpPatch("assignments/{id:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken cancellationToken)
    {
        var result = await _scheduling.ConfirmAsync(id, UserId(), HttpContext.ClientIp(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpPatch("assignments/{id:guid}/cancel")]
    public async Task<IActionResult> CancelAssignment(Guid id, CancellationToken cancellationToken)
    {
        var result = await _scheduling.CancelAssignmentAsync(id, UserId(), HttpContext.ClientIp(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    private string UserId() => User.CurrentUserId() ?? "";

    private static async Task<IActionResult?> ValidateAsync<T>(IValidator<T> validator, T request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        return validation.IsValid ? null : new BadRequestObjectResult(new ValidationProblemDetails(validation.ToDictionary()));
    }
}
