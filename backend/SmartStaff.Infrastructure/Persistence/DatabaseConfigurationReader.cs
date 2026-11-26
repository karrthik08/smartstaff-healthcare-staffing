using Microsoft.Extensions.Configuration;
using SmartStaff.Application.Configuration;

namespace SmartStaff.Infrastructure.Persistence;

public static class DatabaseConfigurationReader
{
    public static DatabaseOptions Read(IConfiguration configuration)
    {
        return DatabaseConfiguration.FromValues(new Dictionary<string, string?>
        {
            ["POSTGRES_HOST"] = configuration["POSTGRES_HOST"],
            ["POSTGRES_PORT"] = configuration["POSTGRES_PORT"],
            ["POSTGRES_DB"] = configuration["POSTGRES_DB"],
            ["POSTGRES_USER"] = configuration["POSTGRES_USER"],
            ["POSTGRES_PASSWORD"] = configuration["POSTGRES_PASSWORD"]
        });
    }

    public static DatabaseOptions ReadFromEnvironment()
    {
        return DatabaseConfiguration.FromValues(new Dictionary<string, string?>
        {
            ["POSTGRES_HOST"] = Environment.GetEnvironmentVariable("POSTGRES_HOST"),
            ["POSTGRES_PORT"] = Environment.GetEnvironmentVariable("POSTGRES_PORT"),
            ["POSTGRES_DB"] = Environment.GetEnvironmentVariable("POSTGRES_DB"),
            ["POSTGRES_USER"] = Environment.GetEnvironmentVariable("POSTGRES_USER"),
            ["POSTGRES_PASSWORD"] = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD")
        });
    }
}
