namespace SmartStaff.Application.Configuration;

public static class DatabaseConfiguration
{
    public static DatabaseOptions FromValues(IReadOnlyDictionary<string, string?> values)
    {
        values.TryGetValue("POSTGRES_PORT", out var portText);
        var portSpecified = !string.IsNullOrWhiteSpace(portText);
        var portIsValid = int.TryParse(portText, out var parsedPort);

        var options = new DatabaseOptions
        {
            Host = ValueOrDefault(values, "POSTGRES_HOST", "localhost"),
            Port = portSpecified && portIsValid ? parsedPort : portSpecified ? 0 : 5433,
            Name = ValueOrDefault(values, "POSTGRES_DB", "smartstaff_db"),
            Username = ValueOrDefault(values, "POSTGRES_USER", "smartstaff"),
            Password = values.TryGetValue("POSTGRES_PASSWORD", out var password) ? password ?? "" : ""
        };

        var validation = new DatabaseOptionsValidator().Validate(options);
        if (!validation.IsValid)
        {
            var message = string.Join(" ", validation.Errors.Select(error => error.ErrorMessage).Distinct());
            throw new InvalidOperationException(message);
        }

        return options;
    }

    private static string ValueOrDefault(
        IReadOnlyDictionary<string, string?> values,
        string key,
        string fallback)
    {
        if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value;
    }
}
