using SmartStaff.API.Authentication;
using SmartStaff.Application;
using SmartStaff.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAuthorization();

var jwtConfigured = JwtAuthenticationExtensions.IsConfigured(builder.Configuration);
if (jwtConfigured)
{
    builder.Services.AddSmartStaffJwt(builder.Configuration);
}

var allowedOrigins = (builder.Configuration["CORS_ORIGINS"] ?? "http://localhost:5173,http://127.0.0.1:5173,http://localhost:8080,http://127.0.0.1:8080")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHttpsRedirection();
}

app.UseCors("Frontend");

if (jwtConfigured)
{
    app.UseAuthentication();
}

app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program;
