using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStaff.API.Authentication;
using SmartStaff.Application.Staffing;

namespace SmartStaff.API.Controllers;

[ApiController]
[Route("api/facilities")]
[Authorize]
public sealed class FacilitiesController : ControllerBase
{
    private readonly IFacilityService _facilities;
    private readonly IValidator<FacilityRequest> _facilityValidator;
    private readonly IValidator<LocationRequest> _locationValidator;
    private readonly IValidator<MembershipRequest> _membershipValidator;

    public FacilitiesController(
        IFacilityService facilities,
        IValidator<FacilityRequest> facilityValidator,
        IValidator<LocationRequest> locationValidator,
        IValidator<MembershipRequest> membershipValidator)
    {
        _facilities = facilities;
        _facilityValidator = facilityValidator;
        _locationValidator = locationValidator;
        _membershipValidator = membershipValidator;
    }

    [HttpPost]
    public async Task<IActionResult> Create(FacilityRequest request, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(_facilityValidator, request, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await _facilities.CreateAsync(request, UserId(), HttpContext.ClientIp(), cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return this.FromFailure(result);
        }

        return CreatedAtAction(nameof(Get), new { id = result.Value.Id }, result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] FacilityListQuery query, CancellationToken cancellationToken)
    {
        var result = await _facilities.ListAsync(query, UserId(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await _facilities.GetAsync(id, UserId(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, FacilityRequest request, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(_facilityValidator, request, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await _facilities.UpdateAsync(id, request, UserId(), HttpContext.ClientIp(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpGet("{id:guid}/locations")]
    public async Task<IActionResult> Locations(Guid id, CancellationToken cancellationToken)
    {
        var result = await _facilities.ListLocationsAsync(id, UserId(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpPost("{id:guid}/locations")]
    public async Task<IActionResult> AddLocation(Guid id, LocationRequest request, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(_locationValidator, request, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await _facilities.AddLocationAsync(id, request, UserId(), HttpContext.ClientIp(), cancellationToken);
        return result.Succeeded ? StatusCode(StatusCodes.Status201Created, result.Value) : this.FromFailure(result);
    }

    [HttpPost("{id:guid}/members")]
    public async Task<IActionResult> AddMember(Guid id, MembershipRequest request, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(_membershipValidator, request, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await _facilities.AddMembershipAsync(id, request, UserId(), HttpContext.ClientIp(), cancellationToken);
        return result.Succeeded ? StatusCode(StatusCodes.Status201Created, result.Value) : this.FromFailure(result);
    }

    private string UserId() => User.CurrentUserId() ?? "";

    private async Task<IActionResult?> ValidateAsync<T>(IValidator<T> validator, T request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        return validation.IsValid ? null : ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
    }
}
