using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using SmartStaff.API.Authentication;
using SmartStaff.Application;
using SmartStaff.Infrastructure;
using SmartStaff.Infrastructure.Identity;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsProduction())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole(options =>
    {
        options.IncludeScopes = true;
        options.TimestampFormat = "O";
    });
}

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<JwtAccessTokenFactory>();
builder.Services.AddSmartStaffJwt(builder.Configuration);

builder.Services.AddAuthorization(options => options.AddSmartStaffPolicies());
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // The load balancer is the only allowed caller in production. Security groups enforce that boundary.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var permitLimit = 20;
if (int.TryParse(builder.Configuration["AUTH_RATE_LIMIT_PER_MINUTE"], out var configuredLimit) && configuredLimit > 0)
{
    permitLimit = configuredLimit;
}

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { title = "Too many requests", detail = "Try again shortly." },
            cancellationToken);
    };
});

var configuredOrigins = builder.Configuration["CORS_ORIGINS"];
if (builder.Environment.IsProduction() && string.IsNullOrWhiteSpace(configuredOrigins))
{
    throw new InvalidOperationException("CORS_ORIGINS is required in production.");
}

var allowedOrigins = (configuredOrigins ?? "http://localhost:5173,http://127.0.0.1:5173,http://localhost:8080,http://127.0.0.1:8080")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

if (builder.Environment.IsProduction())
{
    if (string.Equals(builder.Configuration["PASSWORD_RESET_DEV_SINK"], "true", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("PASSWORD_RESET_DEV_SINK must not be enabled in production.");
    }

    var signingKey = builder.Configuration[JwtAuthenticationExtensions.SigningKeyVariable] ?? "";
    if (signingKey.Contains("not-for-production", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("The development JWT signing key cannot be used in production.");
    }
}

var app = builder.Build();

await IdentityRoleSeeder.SeedAsync(app.Services);
await AdministratorBootstrap.SeedAsync(app.Services, app.Configuration);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseExceptionHandler(handler => handler.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new { title = "Server error", detail = "The request could not be completed." });
    }));
}

app.UseForwardedHeaders();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    if (context.Request.IsHttps)
    {
        context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    }

    await next();
});

if (string.Equals(app.Configuration["HTTPS_REDIRECT"], "true", StringComparison.OrdinalIgnoreCase))
{
    app.UseHttpsRedirection();
}

app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program;
