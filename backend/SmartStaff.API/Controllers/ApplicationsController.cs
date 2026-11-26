using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStaff.API.Authentication;
using SmartStaff.Application.Recruitment;

namespace SmartStaff.API.Controllers;

[ApiController]
[Route("api/applications")]
[Authorize]
public sealed class ApplicationsController : ControllerBase
{
    private readonly IApplicationService _applications;
    private readonly IValidator<ApplicationRequest> _requestValidator;
    private readonly IValidator<ApplicationStatusRequest> _statusValidator;

    public ApplicationsController(
        IApplicationService applications,
        IValidator<ApplicationRequest> requestValidator,
        IValidator<ApplicationStatusRequest> statusValidator)
    {
        _applications = applications;
        _requestValidator = requestValidator;
        _statusValidator = statusValidator;
    }

    [HttpPost]
    public async Task<IActionResult> Apply(ApplicationRequest request, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(_requestValidator, request, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await _applications.ApplyAsync(request, UserId(), HttpContext.ClientIp(), cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return this.FromFailure(result);
        }

        return CreatedAtAction(nameof(Get), new { id = result.Value.Id }, result.Value);
    }

    [HttpGet("me")]
    public async Task<IActionResult> Mine([FromQuery] ApplicationListQuery query, CancellationToken cancellationToken)
    {
        var result = await _applications.ListMineAsync(query, UserId(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken cancellationToken)
    {
        var result = await _applications.SummaryAsync(UserId(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ApplicationListQuery query, CancellationToken cancellationToken)
    {
        var result = await _applications.ListAsync(query, UserId(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await _applications.GetAsync(id, UserId(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, ApplicationStatusRequest request, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(_statusValidator, request, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await _applications.ChangeStatusAsync(id, request, UserId(), HttpContext.ClientIp(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    private string UserId() => User.CurrentUserId() ?? "";

    private static async Task<IActionResult?> ValidateAsync<T>(IValidator<T> validator, T request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (validation.IsValid)
        {
            return null;
        }

        return new BadRequestObjectResult(new ValidationProblemDetails(validation.ToDictionary()));
    }
}
