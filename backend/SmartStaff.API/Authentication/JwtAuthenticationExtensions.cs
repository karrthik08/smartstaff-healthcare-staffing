using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SmartStaff.Infrastructure.Persistence;

namespace SmartStaff.API.Authentication;

public static class JwtAuthenticationExtensions
{
    public const string SigningKeyVariable = "JWT_SIGNING_KEY";

    public static bool IsConfigured(IConfiguration configuration)
    {
        return !string.IsNullOrWhiteSpace(configuration[SigningKeyVariable]);
    }

    public static IServiceCollection AddSmartStaffJwt(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var signingKey = configuration[SigningKeyVariable]
            ?? throw new InvalidOperationException("JWT_SIGNING_KEY environment variable is required.");

        if (Encoding.UTF8.GetByteCount(signingKey) < 32)
        {
            throw new InvalidOperationException("JWT_SIGNING_KEY must be at least 32 bytes.");
        }

        var issuer = configuration["JWT_ISSUER"] ?? "SmartStaff";
        var audience = configuration["JWT_AUDIENCE"] ?? "SmartStaff";

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ClockSkew = TimeSpan.FromMinutes(1),
                    RoleClaimType = "role",
                    NameClaimType = JwtRegisteredClaimNames.Email
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var principal = context.Principal;
                        var userId = principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                        var stamp = principal?.FindFirst(JwtAccessTokenFactory.SecurityStampClaim)?.Value;
                        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(stamp))
                        {
                            context.Fail("The access token is no longer valid.");
                            return;
                        }

                        var database = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                        var current = await database.Users.AsNoTracking()
                            .Where(user => user.Id == userId)
                            .Select(user => new { user.SecurityStamp, user.IsActive })
                            .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
                        if (current is null || !current.IsActive || !string.Equals(current.SecurityStamp, stamp, StringComparison.Ordinal))
                        {
                            context.Fail("The access token is no longer valid.");
                        }
                    }
                };
            });

        return services;
    }
}
