using Npgsql;
using SmartStaff.Application.Configuration;

namespace SmartStaff.Infrastructure.Persistence;

internal static class NpgsqlConnectionStringFactory
{
    private static int Positive(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out var value) && value > 0 ? value : fallback;

    private static int NonNegative(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out var value) && value >= 0 ? value : fallback;
    public static string Create(DatabaseOptions options)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = options.Host,
            Port = options.Port,
            Database = options.Name,
            Username = options.Username,
            Password = options.Password,
            MaxPoolSize = Positive("POSTGRES_MAX_POOL_SIZE", 20),
            MinPoolSize = NonNegative("POSTGRES_MIN_POOL_SIZE", 0),
            Timeout = Positive("POSTGRES_CONNECT_TIMEOUT", 15),
            ConnectionIdleLifetime = Positive("POSTGRES_CONNECTION_IDLE_SECONDS", 60)
        };
        return builder.ConnectionString;
    }
}
