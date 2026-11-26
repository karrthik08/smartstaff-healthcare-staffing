using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStaff.API.Authentication;
using SmartStaff.Application.Authentication;

namespace SmartStaff.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = AuthorizationPolicies.Administrator)]
public sealed class AdminController : ControllerBase
{
    private readonly IUserAdminService _admin;
    private readonly IValidator<UpdateRolesRequest> _rolesValidator;
    private readonly IValidator<UpdateAccountStatusRequest> _statusValidator;

    public AdminController(
        IUserAdminService admin,
        IValidator<UpdateRolesRequest> rolesValidator,
        IValidator<UpdateAccountStatusRequest> statusValidator)
    {
        _admin = admin;
        _rolesValidator = rolesValidator;
        _statusValidator = statusValidator;
    }

    [HttpGet("users")]
    public async Task<IActionResult> Users(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] bool? active,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _admin.ListUsersAsync(new UserListQuery(search, role, active, page, pageSize), cancellationToken);
        return Ok(result.Value);
    }

    [HttpPut("users/{id}/roles")]
    public async Task<IActionResult> UpdateRoles(string id, UpdateRolesRequest request, CancellationToken cancellationToken)
    {
        var validation = await _rolesValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        var actorId = User.CurrentUserId();
        if (string.IsNullOrEmpty(actorId))
        {
            return Unauthorized();
        }

        var result = await _admin.UpdateRolesAsync(actorId, id, request, HttpContext.ClientIp(), cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return this.FromFailure(result);
        }

        return Ok(result.Value);
    }

    [HttpPut("users/{id}/status")]
    public async Task<IActionResult> UpdateStatus(string id, UpdateAccountStatusRequest request, CancellationToken cancellationToken)
    {
        var validation = await _statusValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        var actorId = User.CurrentUserId();
        if (string.IsNullOrEmpty(actorId))
        {
            return Unauthorized();
        }

        var result = await _admin.SetActiveAsync(actorId, id, request.Active!.Value, HttpContext.ClientIp(), cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return this.FromFailure(result);
        }

        return Ok(result.Value);
    }

    [HttpGet("audit-logs")]
    public async Task<IActionResult> AuditLogs(CancellationToken cancellationToken)
    {
        var result = await _admin.ListAuditLogsAsync(cancellationToken);
        return Ok(result.Value);
    }
}
