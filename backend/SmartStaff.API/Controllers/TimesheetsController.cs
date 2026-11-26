using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStaff.API.Authentication;
using SmartStaff.Application.Timekeeping;

namespace SmartStaff.API.Controllers;

[ApiController]
[Route("api/timesheets")]
[Authorize]
public sealed class TimesheetsController : ControllerBase
{
    private readonly ITimekeepingService _timekeeping;
    private readonly IValidator<TimesheetRequest> _timesheetValidator;
    private readonly IValidator<TimesheetEntryRequest> _entryValidator;
    private readonly IValidator<RejectTimesheetRequest> _rejectValidator;
    private readonly IValidator<AdjustmentRequest> _adjustmentValidator;
    private readonly IValidator<OvertimePolicyRequest> _policyValidator;

    public TimesheetsController(
        ITimekeepingService timekeeping,
        IValidator<TimesheetRequest> timesheetValidator,
        IValidator<TimesheetEntryRequest> entryValidator,
        IValidator<RejectTimesheetRequest> rejectValidator,
        IValidator<AdjustmentRequest> adjustmentValidator,
        IValidator<OvertimePolicyRequest> policyValidator)
    {
        _timekeeping = timekeeping;
        _timesheetValidator = timesheetValidator;
        _entryValidator = entryValidator;
        _rejectValidator = rejectValidator;
        _adjustmentValidator = adjustmentValidator;
        _policyValidator = policyValidator;
    }

    [HttpPost]
    public async Task<IActionResult> Create(TimesheetRequest request, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(_timesheetValidator, request, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await _timekeeping.CreateTimesheetAsync(request, UserId(), HttpContext.ClientIp(), cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return this.FromFailure(result);
        }

        return result.Value.Created
            ? CreatedAtAction(nameof(Get), new { id = result.Value.Id }, result.Value)
            : Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] TimesheetListQuery query, CancellationToken cancellationToken)
    {
        var result = await _timekeeping.ListTimesheetsAsync(query, UserId(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken cancellationToken)
    {
        var result = await _timekeeping.SummaryAsync(UserId(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await _timekeeping.GetTimesheetAsync(id, UserId(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> History(Guid id, CancellationToken cancellationToken)
    {
        var result = await _timekeeping.HistoryAsync(id, UserId(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpPost("{id:guid}/entries")]
    public async Task<IActionResult> AddEntry(Guid id, TimesheetEntryRequest request, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(_entryValidator, request, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await _timekeeping.AddEntryAsync(id, request, UserId(), HttpContext.ClientIp(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpPut("{id:guid}/entries/{entryId:guid}")]
    public async Task<IActionResult> UpdateEntry(Guid id, Guid entryId, TimesheetEntryRequest request, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(_entryValidator, request, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await _timekeeping.UpdateEntryAsync(id, entryId, request, UserId(), HttpContext.ClientIp(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
    {
        var result = await _timekeeping.SubmitAsync(id, UserId(), HttpContext.ClientIp(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        var result = await _timekeeping.ApproveAsync(id, UserId(), HttpContext.ClientIp(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, RejectTimesheetRequest request, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(_rejectValidator, request, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await _timekeeping.RejectAsync(id, request, UserId(), HttpContext.ClientIp(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpPost("/api/timesheet-adjustments")]
    public async Task<IActionResult> RequestAdjustment(AdjustmentRequest request, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(_adjustmentValidator, request, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await _timekeeping.RequestAdjustmentAsync(request, UserId(), HttpContext.ClientIp(), cancellationToken);
        return result.Succeeded ? StatusCode(StatusCodes.Status201Created, result.Value) : this.FromFailure(result);
    }

    [HttpPatch("/api/timesheet-adjustments/{id:guid}")]
    public async Task<IActionResult> ReviewAdjustment(Guid id, ReviewAdjustmentRequest request, CancellationToken cancellationToken)
    {
        var result = await _timekeeping.ReviewAdjustmentAsync(id, request, UserId(), HttpContext.ClientIp(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpPost("/api/overtime-policies")]
    public async Task<IActionResult> SavePolicy(OvertimePolicyRequest request, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(_policyValidator, request, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await _timekeeping.SavePolicyAsync(request, UserId(), HttpContext.ClientIp(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    private string UserId() => User.CurrentUserId() ?? "";

    private static async Task<IActionResult?> ValidateAsync<T>(IValidator<T> validator, T request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        return validation.IsValid ? null : new BadRequestObjectResult(new ValidationProblemDetails(validation.ToDictionary()));
    }
}
