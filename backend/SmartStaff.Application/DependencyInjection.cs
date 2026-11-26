using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SmartStaff.Application.Configuration;

namespace SmartStaff.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<DatabaseOptionsValidator>();
        return services;
    }
}
