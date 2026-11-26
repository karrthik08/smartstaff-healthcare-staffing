using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartStaff.Application.Authentication;
using SmartStaff.Infrastructure.Persistence;

namespace SmartStaff.Infrastructure.Identity;

public static class AdministratorBootstrap
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        var email = configuration["BOOTSTRAP_ADMIN_EMAIL"]?.Trim();
        var password = configuration["BOOTSTRAP_ADMIN_PASSWORD"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        using var scope = services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditWriter>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AdministratorBootstrap");
        var existingAdministrators = await users.GetUsersInRoleAsync(AppRoles.Administrator);
        if (existingAdministrators.Count > 0)
        {
            return;
        }

        var existing = await users.FindByEmailAsync(email);
        if (existing is not null)
        {
            await audit.WriteAsync("AdministratorBootstrapSkipped", existing.Id, false, "Existing account was not promoted", null, CancellationToken.None);
            logger.LogWarning("Administrator bootstrap skipped because the email is already registered.");
            return;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = configuration["BOOTSTRAP_ADMIN_FIRST_NAME"]?.Trim() is { Length: > 0 } first ? first : "Administrator",
            LastName = configuration["BOOTSTRAP_ADMIN_LAST_NAME"]?.Trim() is { Length: > 0 } last ? last : "Account",
            IsActive = true
        };
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            logger.LogError("Administrator bootstrap failed because the account details were rejected.");
            return;
        }

        var roleResult = await users.AddToRoleAsync(user, AppRoles.Administrator);
        if (!roleResult.Succeeded)
        {
            logger.LogError("Administrator bootstrap could not assign the administrator role for user {UserId}.", user.Id);
            return;
        }

        await audit.WriteAsync("AdministratorBootstrapped", user.Id, true, null, null, CancellationToken.None);
        logger.LogInformation("Administrator bootstrap created user {UserId}.", user.Id);
    }
}
