using Npgsql;
using SmartStaff.Application.Configuration;

namespace SmartStaff.Infrastructure.Persistence;

internal static class NpgsqlConnectionStringFactory
{
    public static string Create(DatabaseOptions options)
    {
        return new NpgsqlConnectionStringBuilder
        {
            Host = options.Host,
            Port = options.Port,
            Database = options.Name,
            Username = options.Username,
            Password = options.Password
        }.ConnectionString;
    }
}
