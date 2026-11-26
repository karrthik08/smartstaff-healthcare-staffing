using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStaff.API.Authentication;
using SmartStaff.Application.Recruitment;

namespace SmartStaff.API.Controllers;

[ApiController]
[Route("api/placements")]
[Authorize]
public sealed class PlacementsController : ControllerBase
{
    private readonly IPlacementService _placements;
    private readonly IValidator<PlacementRequest> _requestValidator;
    private readonly IValidator<PlacementStatusRequest> _statusValidator;

    public PlacementsController(
        IPlacementService placements,
        IValidator<PlacementRequest> requestValidator,
        IValidator<PlacementStatusRequest> statusValidator)
    {
        _placements = placements;
        _requestValidator = requestValidator;
        _statusValidator = statusValidator;
    }

    [HttpPost]
    public async Task<IActionResult> Create(PlacementRequest request, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(_requestValidator, request, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await _placements.CreateAsync(request, UserId(), HttpContext.ClientIp(), cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return this.FromFailure(result);
        }

        return CreatedAtAction(nameof(Get), new { id = result.Value.Id }, result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] PlacementListQuery query, CancellationToken cancellationToken)
    {
        var result = await _placements.ListAsync(query, UserId(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await _placements.GetAsync(id, UserId(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, PlacementStatusRequest request, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(_statusValidator, request, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await _placements.ChangeStatusAsync(id, request, UserId(), HttpContext.ClientIp(), cancellationToken);
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
