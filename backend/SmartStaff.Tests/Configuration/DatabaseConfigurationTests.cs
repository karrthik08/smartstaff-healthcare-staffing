using SmartStaff.Application.Configuration;

namespace SmartStaff.Tests.Configuration;

public sealed class DatabaseConfigurationTests
{
    [Test]
    public void FromValues_UsesLocalDevelopmentDefaults()
    {
        var options = DatabaseConfiguration.FromValues(new Dictionary<string, string?>
        {
            ["POSTGRES_PASSWORD"] = "test-only-password"
        });

        Assert.That(options.Host, Is.EqualTo("localhost"));
        Assert.That(options.Port, Is.EqualTo(5433));
        Assert.That(options.Name, Is.EqualTo("smartstaff_db"));
        Assert.That(options.Username, Is.EqualTo("smartstaff"));
        Assert.That(options.Password, Is.EqualTo("test-only-password"));
    }

    [Test]
    public void FromValues_RequiresPasswordWithoutEchoingIt()
    {
        const string secret = "should-not-appear-in-the-exception";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            DatabaseConfiguration.FromValues(new Dictionary<string, string?>
            {
                ["POSTGRES_PASSWORD"] = "",
                ["POSTGRES_HOST"] = secret
            }));

        Assert.That(exception!.Message, Does.Contain("POSTGRES_PASSWORD"));
        Assert.That(exception.Message, Does.Not.Contain(secret));
    }

    [Test]
    public void FromValues_RejectsAnInvalidPort()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            DatabaseConfiguration.FromValues(new Dictionary<string, string?>
            {
                ["POSTGRES_PORT"] = "not-a-port",
                ["POSTGRES_PASSWORD"] = "test-only-password"
            }));

        Assert.That(exception!.Message, Does.Contain("POSTGRES_PORT"));
    }
}
