using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartStaff.Application.Authentication;
using SmartStaff.Application.Health;
using SmartStaff.Application.Staffing;
using SmartStaff.Infrastructure.Health;
using SmartStaff.Infrastructure.Identity;
using SmartStaff.Infrastructure.Persistence;
using SmartStaff.Application.Recruitment;
using SmartStaff.Application.Scheduling;
using SmartStaff.Application.Notifications;
using SmartStaff.Application.Timekeeping;
using SmartStaff.Infrastructure.Notifications;
using SmartStaff.Infrastructure.Recruitment;
using SmartStaff.Infrastructure.Scheduling;
using SmartStaff.Infrastructure.Timekeeping;
using SmartStaff.Infrastructure.Staffing;

namespace SmartStaff.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var database = DatabaseConfigurationReader.Read(configuration);

        services.AddSingleton<NotificationOutboxInterceptor>();
        services.AddDbContext<ApplicationDbContext>((provider, options) =>
            options.UseNpgsql(NpgsqlConnectionStringFactory.Create(database))
                .AddInterceptors(provider.GetRequiredService<NotificationOutboxInterceptor>()));

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.Configure<DataProtectionTokenProviderOptions>(options =>
        {
            options.TokenLifespan = TimeSpan.FromMinutes(30);
        });

        services.AddScoped<IHealthService, HealthService>();
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProfileService, AuthService>();
        services.AddScoped<IUserAdminService, AuthService>();
        services.AddScoped<IFacilityService, FacilityService>();
        services.AddScoped<IRequisitionService, RequisitionService>();
        services.AddScoped<IClinicianService, ClinicianService>();
        services.AddScoped<IApplicationService, ApplicationService>();
        services.AddScoped<IPlacementService, PlacementService>();
        services.AddScoped<ISchedulingService, SchedulingService>();
        services.AddScoped<ITimekeepingService, TimekeepingService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationProcessor, NotificationProcessor>();
        services.AddScoped<IEmailSender, RecordingEmailSender>();
        services.AddHostedService<NotificationHostedService>();
        if (string.Equals(configuration["PASSWORD_RESET_DEV_SINK"], "true", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IPasswordResetNotifier, DevelopmentPasswordResetNotifier>();
        }
        else
        {
            services.AddScoped<IPasswordResetNotifier, NullPasswordResetNotifier>();
        }

        return services;
    }
}
