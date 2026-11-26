using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartStaff.Application.Health;
using SmartStaff.Infrastructure.Health;
using SmartStaff.Infrastructure.Persistence;

namespace SmartStaff.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var database = DatabaseConfigurationReader.Read(configuration);

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(NpgsqlConnectionStringFactory.Create(database)));

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        services.AddScoped<IHealthService, HealthService>();
        return services;
    }
}
