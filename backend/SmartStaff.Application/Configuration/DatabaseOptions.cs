namespace SmartStaff.Application.Configuration;

public sealed class DatabaseOptions
{
    public string Host { get; init; } = "localhost";

    public int Port { get; init; } = 5433;

    public string Name { get; init; } = "smartstaff_db";

    public string Username { get; init; } = "smartstaff";

    public string Password { get; init; } = "";
}
