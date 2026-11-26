using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SmartStaff.Application.Authentication;
using SmartStaff.Infrastructure.Persistence;

namespace SmartStaff.Infrastructure.Identity;

public static class IdentityRoleSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in AppRoles.Assignable)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }
}
