using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

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
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });

        return services;
    }
}
