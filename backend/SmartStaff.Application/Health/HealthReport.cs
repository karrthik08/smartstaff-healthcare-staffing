namespace SmartStaff.Application.Health;

public sealed record HealthReport(
    string Status,
    string Api,
    string Database,
    DateTimeOffset CheckedAtUtc,
    string? DatabaseError)
{
    public const string HealthyStatus = "Healthy";
    public const string UnhealthyStatus = "Unhealthy";

    public static HealthReport FromDatabase(bool connected, string? error, DateTimeOffset checkedAtUtc)
    {
        return new HealthReport(
            connected ? HealthyStatus : UnhealthyStatus,
            "SmartStaff.API",
            connected ? "Connected" : "Unavailable",
            checkedAtUtc,
            connected ? null : Sanitize(error));
    }

    private static string? Sanitize(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "PostgreSQL did not accept a connection.";
        }

        if (message.Contains("Password=", StringComparison.OrdinalIgnoreCase))
        {
            return "Database connection failed.";
        }

        return message;
    }
}
