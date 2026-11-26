using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartStaff.API.Authentication;
using SmartStaff.Application.Authentication;
using SmartStaff.Infrastructure.Identity;

namespace SmartStaff.API.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly JwtAccessTokenFactory _tokens;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<ForgotPasswordRequest> _forgotValidator;
    private readonly IValidator<ResetPasswordRequest> _resetValidator;

    public AuthController(
        IAuthService auth,
        JwtAccessTokenFactory tokens,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator,
        IValidator<ForgotPasswordRequest> forgotValidator,
        IValidator<ResetPasswordRequest> resetValidator)
    {
        _auth = auth;
        _tokens = tokens;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _forgotValidator = forgotValidator;
        _resetValidator = resetValidator;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var validation = await _registerValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        var result = await _auth.RegisterAsync(request, HttpContext.ClientIp(), cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return this.FromFailure(result);
        }

        return StatusCode(StatusCodes.Status201Created, new { user = result.Value });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var validation = await _loginValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        var result = await _auth.LoginAsync(request, HttpContext.ClientIp(), cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return this.FromFailure(result);
        }

        return Session(result.Value);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        if (!AuthCookies.CsrfMatches(Request))
        {
            return Problem(title: "CSRF validation failed", detail: "Refresh the page and try again.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!Request.Cookies.TryGetValue(AuthCookies.RefreshName, out var refreshToken))
        {
            return Problem(title: "Session expired", detail: "Sign in again.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var result = await _auth.RefreshAsync(refreshToken, HttpContext.ClientIp(), cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            AuthCookies.Clear(Response, Request);
            return this.FromFailure(result);
        }

        return Session(result.Value);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (!AuthCookies.CsrfMatches(Request))
        {
            return Problem(title: "CSRF validation failed", detail: "Refresh the page and try again.", statusCode: StatusCodes.Status400BadRequest);
        }

        Request.Cookies.TryGetValue(AuthCookies.RefreshName, out var refreshToken);
        await _auth.LogoutAsync(refreshToken, HttpContext.ClientIp(), cancellationToken);
        AuthCookies.Clear(Response, Request);
        return NoContent();
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var validation = await _forgotValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        await _auth.ForgotPasswordAsync(request, HttpContext.ClientIp(), cancellationToken);
        return Ok(new { message = "If an account exists for that email, password reset instructions have been created." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var validation = await _resetValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        var result = await _auth.ResetPasswordAsync(request, HttpContext.ClientIp(), cancellationToken);
        if (!result.Succeeded)
        {
            return this.FromFailure(result);
        }

        return Ok(new { message = "Your password has been reset." });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var userId = User.CurrentUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var result = await _auth.GetUserAsync(userId, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return this.FromFailure(result);
        }

        return Ok(result.Value);
    }

    private IActionResult Session(AuthSession session)
    {
        var csrf = TokenHasher.CreateSecret();
        AuthCookies.Write(Response, Request, session.RefreshToken, session.RefreshTokenExpiresAt, csrf);
        var access = _tokens.Create(session.User);
        return Ok(new
        {
            accessToken = access.Token,
            expiresAt = access.ExpiresAt,
            user = session.User
        });
    }
}
