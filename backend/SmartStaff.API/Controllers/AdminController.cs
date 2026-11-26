using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStaff.API.Authentication;
using SmartStaff.Application.Authentication;

namespace SmartStaff.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = AppRoles.Administrator)]
public sealed class AdminController : ControllerBase
{
    private readonly IUserAdminService _admin;
    private readonly IValidator<UpdateRolesRequest> _rolesValidator;

    public AdminController(IUserAdminService admin, IValidator<UpdateRolesRequest> rolesValidator)
    {
        _admin = admin;
        _rolesValidator = rolesValidator;
    }

    [HttpGet("users")]
    public async Task<IActionResult> Users(CancellationToken cancellationToken)
    {
        var result = await _admin.ListUsersAsync(cancellationToken);
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

    [HttpGet("audit-logs")]
    public async Task<IActionResult> AuditLogs(CancellationToken cancellationToken)
    {
        var result = await _admin.ListAuditLogsAsync(cancellationToken);
        return Ok(result.Value);
    }
}
