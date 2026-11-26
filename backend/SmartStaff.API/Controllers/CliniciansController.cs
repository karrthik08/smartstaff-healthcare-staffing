using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStaff.API.Authentication;
using SmartStaff.Application.Recruitment;

namespace SmartStaff.API.Controllers;

[ApiController]
[Route("api/clinicians")]
[Authorize]
public sealed class CliniciansController : ControllerBase
{
    private readonly IClinicianService _clinicians;
    private readonly IValidator<ClinicianProfileRequest> _profileValidator;
    private readonly IValidator<CredentialRequest> _credentialValidator;
    private readonly IValidator<CredentialVerificationRequest> _verificationValidator;

    public CliniciansController(
        IClinicianService clinicians,
        IValidator<ClinicianProfileRequest> profileValidator,
        IValidator<CredentialRequest> credentialValidator,
        IValidator<CredentialVerificationRequest> verificationValidator)
    {
        _clinicians = clinicians;
        _profileValidator = profileValidator;
        _credentialValidator = credentialValidator;
        _verificationValidator = verificationValidator;
    }

    [HttpGet("me")]
    public async Task<IActionResult> Mine(CancellationToken cancellationToken)
    {
        var result = await _clinicians.GetMineAsync(UserId(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpPut("me")]
    public async Task<IActionResult> Save(ClinicianProfileRequest request, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(_profileValidator, request, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await _clinicians.SaveMineAsync(request, UserId(), HttpContext.ClientIp(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ClinicianListQuery query, CancellationToken cancellationToken)
    {
        var result = await _clinicians.ListAsync(query, UserId(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await _clinicians.GetAsync(id, UserId(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpPost("me/credentials")]
    public async Task<IActionResult> AddCredential(CredentialRequest request, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(_credentialValidator, request, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await _clinicians.AddCredentialAsync(request, UserId(), HttpContext.ClientIp(), cancellationToken);
        return result.Succeeded ? StatusCode(StatusCodes.Status201Created, result.Value) : this.FromFailure(result);
    }

    [HttpGet("me/credentials")]
    public async Task<IActionResult> Credentials(CancellationToken cancellationToken)
    {
        var result = await _clinicians.ListCredentialsAsync(UserId(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpPut("me/credentials/{id:guid}")]
    public async Task<IActionResult> UpdateCredential(Guid id, CredentialRequest request, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(_credentialValidator, request, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await _clinicians.UpdateCredentialAsync(id, request, UserId(), HttpContext.ClientIp(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpDelete("me/credentials/{id:guid}")]
    public async Task<IActionResult> DeleteCredential(Guid id, CancellationToken cancellationToken)
    {
        var result = await _clinicians.DeleteCredentialAsync(id, UserId(), HttpContext.ClientIp(), cancellationToken);
        return result.Succeeded ? NoContent() : this.FromFailure(result);
    }

    [HttpPatch("{id:guid}/credentials/{credentialId:guid}/verification")]
    public async Task<IActionResult> Verify(Guid id, Guid credentialId, CredentialVerificationRequest request, CancellationToken cancellationToken)
    {
        var invalid = await ValidateAsync(_verificationValidator, request, cancellationToken);
        if (invalid is not null)
        {
            return invalid;
        }

        var result = await _clinicians.VerifyCredentialAsync(id, credentialId, request, UserId(), HttpContext.ClientIp(), cancellationToken);
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

        var problem = new ValidationProblemDetails(validation.ToDictionary());
        return new BadRequestObjectResult(problem);
    }
}
