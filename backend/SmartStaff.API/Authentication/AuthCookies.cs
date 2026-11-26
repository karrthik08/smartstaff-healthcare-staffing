using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;
using SmartStaff.Application.Common;
using SmartStaff.Infrastructure.Identity;

namespace SmartStaff.API.Authentication;

public static class AuthCookies
{
    public const string RefreshName = "smartstaff.refresh";
    public const string CsrfName = "smartstaff.csrf";
    public const string CsrfHeader = "X-CSRF-TOKEN";

    public static void Write(HttpResponse response, HttpRequest request, string refreshToken, DateTimeOffset refreshExpires, string csrfToken)
    {
        response.Cookies.Append(RefreshName, refreshToken, RefreshOptions(request, refreshExpires));
        response.Cookies.Append(CsrfName, csrfToken, CsrfOptions(request));
    }

    public static void Clear(HttpResponse response, HttpRequest request)
    {
        response.Cookies.Delete(RefreshName, RefreshOptions(request, DateTimeOffset.UnixEpoch));
        response.Cookies.Delete(CsrfName, CsrfOptions(request));
    }

    public static bool CsrfMatches(HttpRequest request)
    {
        var cookie = request.Cookies[CsrfName];
        var header = request.Headers[CsrfHeader].ToString();
        if (string.IsNullOrEmpty(cookie) || cookie.Length != header.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(cookie), Encoding.UTF8.GetBytes(header));
    }

    private static CookieOptions RefreshOptions(HttpRequest request, DateTimeOffset expires) => new()
    {
        HttpOnly = true,
        Secure = request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = "/api/auth",
        Expires = expires
    };

    private static CookieOptions CsrfOptions(HttpRequest request) => new()
    {
        HttpOnly = false,
        Secure = request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = "/"
    };
}

public static class ApiResults
{
    public static IActionResult FromFailure<T>(this ControllerBase controller, OperationResult<T> result) =>
        controller.Problem(title: result.Title, detail: result.Detail, statusCode: result.StatusCode);

    public static string? ClientIp(this HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString();

    public static string? CurrentUserId(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
}
