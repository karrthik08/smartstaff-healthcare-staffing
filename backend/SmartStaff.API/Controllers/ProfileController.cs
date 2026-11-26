using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStaff.API.Authentication;
using SmartStaff.Application.Authentication;

namespace SmartStaff.API.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize]
public sealed class ProfileController : ControllerBase
{
    private readonly IProfileService _profiles;
    private readonly IValidator<UpdateProfileRequest> _validator;
    private readonly IValidator<ChangePasswordRequest> _passwordValidator;

    public ProfileController(
        IProfileService profiles,
        IValidator<UpdateProfileRequest> validator,
        IValidator<ChangePasswordRequest> passwordValidator)
    {
        _profiles = profiles;
        _validator = validator;
        _passwordValidator = passwordValidator;
    }

    [HttpPut]
    public async Task<IActionResult> Update(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        var userId = User.CurrentUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var result = await _profiles.UpdateAsync(userId, request, HttpContext.ClientIp(), cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return this.FromFailure(result);
        }

        return Ok(result.Value);
    }

    [HttpPost("password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var validation = await _passwordValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        var userId = User.CurrentUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var result = await _profiles.ChangePasswordAsync(userId, request, HttpContext.ClientIp(), cancellationToken);
        if (!result.Succeeded)
        {
            return this.FromFailure(result);
        }

        return Ok(new { message = "Your password has been changed." });
    }
}
