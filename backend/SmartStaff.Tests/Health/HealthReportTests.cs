using SmartStaff.Application.Health;

namespace SmartStaff.Tests.Health;

public sealed class HealthReportTests
{
    [Test]
    public void FromDatabase_ReportsALiveConnection()
    {
        var checkedAt = new DateTimeOffset(2026, 11, 25, 18, 0, 0, TimeSpan.Zero);

        var report = HealthReport.FromDatabase(true, "ignored", checkedAt);

        Assert.That(report.Status, Is.EqualTo(HealthReport.HealthyStatus));
        Assert.That(report.Api, Is.EqualTo("SmartStaff.API"));
        Assert.That(report.Database, Is.EqualTo("Connected"));
        Assert.That(report.DatabaseError, Is.Null);
        Assert.That(report.CheckedAtUtc, Is.EqualTo(checkedAt));
    }

    [Test]
    public void FromDatabase_HidesConnectionStrings()
    {
        var report = HealthReport.FromDatabase(
            false,
            "Host=localhost;Password=secret",
            DateTimeOffset.UnixEpoch);

        Assert.That(report.Status, Is.EqualTo(HealthReport.UnhealthyStatus));
        Assert.That(report.Database, Is.EqualTo("Unavailable"));
        Assert.That(report.DatabaseError, Is.EqualTo("Database connection failed."));
        Assert.That(report.DatabaseError, Does.Not.Contain("secret"));
    }
}
