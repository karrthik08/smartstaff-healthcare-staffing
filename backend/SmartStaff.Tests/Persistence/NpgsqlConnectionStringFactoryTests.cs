using Npgsql;
using SmartStaff.Application.Configuration;
using SmartStaff.Infrastructure.Persistence;

namespace SmartStaff.Tests.Persistence;

public sealed class NpgsqlConnectionStringFactoryTests
{
    [Test]
    public void Create_KeepsSpecialCharactersInsideThePassword()
    {
        var connectionString = NpgsqlConnectionStringFactory.Create(new DatabaseOptions
        {
            Host = "localhost",
            Port = 5433,
            Name = "smartstaff_db",
            Username = "smartstaff",
            Password = "p@ss word"
        });

        var parsed = new NpgsqlConnectionStringBuilder(connectionString);

        Assert.That(parsed.Host, Is.EqualTo("localhost"));
        Assert.That(parsed.Port, Is.EqualTo(5433));
        Assert.That(parsed.Database, Is.EqualTo("smartstaff_db"));
        Assert.That(parsed.Username, Is.EqualTo("smartstaff"));
        Assert.That(parsed.Password, Is.EqualTo("p@ss word"));
    }
}
