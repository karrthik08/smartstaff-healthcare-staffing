using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartStaff.Infrastructure.Persistence;

namespace SmartStaff.Tests;

public static class TestDatabase
{
    public const string Name = "smartstaff_test";

    public static async Task PrepareAsync()
    {
        var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        Environment.SetEnvironmentVariable("POSTGRES_DB", Name);
        Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_EMAIL", null);
        Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_PASSWORD", null);
        await CreateIfMissingAsync(password);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(NpgsqlConnectionStringFactory.Create(DatabaseConfigurationReader.ReadFromEnvironment()))
            .Options;
        await using var context = new ApplicationDbContext(options);
        await context.Database.MigrateAsync();
    }

    private static async Task CreateIfMissingAsync(string password)
    {
        var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5433";
        var user = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "smartstaff";
        var connectionString = $"Host={host};Port={port};Database=postgres;Username={user};Password={password}";
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var exists = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @name", connection);
        exists.Parameters.AddWithValue("name", Name);
        if (await exists.ExecuteScalarAsync() is not null)
        {
            return;
        }

        await using var create = new NpgsqlCommand($"CREATE DATABASE {Name}", connection);
        await create.ExecuteNonQueryAsync();
    }
}
