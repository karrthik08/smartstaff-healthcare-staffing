using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SmartStaff.Application.Authentication;

namespace SmartStaff.API.Authentication;

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

public sealed class JwtAccessTokenFactory
{
    private readonly IConfiguration _configuration;

    public JwtAccessTokenFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public AccessToken Create(AuthenticatedUserDto user)
    {
        var signingKey = _configuration[JwtAuthenticationExtensions.SigningKeyVariable]
            ?? throw new InvalidOperationException("JWT_SIGNING_KEY environment variable is required.");
        var minutes = 15;
        if (int.TryParse(_configuration["JWT_ACCESS_TOKEN_MINUTES"], out var configured) && configured is > 0 and <= 60)
        {
            minutes = configured;
        }

        var expires = DateTimeOffset.UtcNow.AddMinutes(minutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.GivenName, user.FirstName),
            new(JwtRegisteredClaimNames.FamilyName, user.LastName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        claims.AddRange(user.Roles.Select(role => new Claim("role", role)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _configuration["JWT_ISSUER"] ?? "SmartStaff",
            audience: _configuration["JWT_AUDIENCE"] ?? "SmartStaff",
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
