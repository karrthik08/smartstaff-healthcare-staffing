using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStaff.API.Authentication;
using SmartStaff.Application.Staffing;

namespace SmartStaff.API.Controllers;

[ApiController]
[Route("api/requisitions")]
[Authorize]
public sealed class RequisitionsController : ControllerBase
{
    private readonly IRequisitionService _requisitions;
    private readonly IValidator<RequisitionRequest> _requestValidator;
    private readonly IValidator<RequisitionStatusRequest> _statusValidator;
    private readonly IValidator<AssignRecruiterRequest> _assignValidator;

    public RequisitionsController(
        IRequisitionService requisitions,
        IValidator<RequisitionRequest> requestValidator,
        IValidator<RequisitionStatusRequest> statusValidator,
        IValidator<AssignRecruiterRequest> assignValidator)
    {
        _requisitions = requisitions;
        _requestValidator = requestValidator;
        _statusValidator = statusValidator;
        _assignValidator = assignValidator;
    }

    [HttpPost]
    public async Task<IActionResult> Create(RequisitionRequest request, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(_requestValidator, request, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await _requisitions.CreateAsync(request, UserId(), HttpContext.ClientIp(), cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return this.FromFailure(result);
        }

        return CreatedAtAction(nameof(Get), new { id = result.Value.Id }, result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] RequisitionListQuery query, CancellationToken cancellationToken)
    {
        var result = await _requisitions.ListAsync(query, UserId(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await _requisitions.GetAsync(id, UserId(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, RequisitionRequest request, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(_requestValidator, request, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await _requisitions.UpdateAsync(id, request, UserId(), HttpContext.ClientIp(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, RequisitionStatusRequest request, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(_statusValidator, request, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await _requisitions.ChangeStatusAsync(id, request, UserId(), HttpContext.ClientIp(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpPatch("{id:guid}/assign-recruiter")]
    public async Task<IActionResult> Assign(Guid id, AssignRecruiterRequest request, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(_assignValidator, request, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await _requisitions.AssignRecruiterAsync(id, request, UserId(), HttpContext.ClientIp(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    private string UserId() => User.CurrentUserId() ?? "";

    private async Task<IActionResult?> ValidateAsync<T>(IValidator<T> validator, T request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        return validation.IsValid ? null : ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
    }
}
