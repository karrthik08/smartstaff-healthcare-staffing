using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SmartStaff.Application.Health;

namespace SmartStaff.Tests.Health;

public sealed class HealthEndpointTests
{
    [Test]
    public async Task GetHealth_ReadsPostgreSqlThroughTheApi()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("POSTGRES_PASSWORD")))
        {
            Assert.Ignore("POSTGRES_PASSWORD is not set, so the live database check was skipped.");
        }

        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");
        var report = await response.Content.ReadFromJsonAsync<HealthReport>();

        Assert.That(report, Is.Not.Null);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(report!.Status, Is.EqualTo(HealthReport.HealthyStatus));
        Assert.That(report.Database, Is.EqualTo("Connected"));
        Assert.That(report.Api, Is.EqualTo("SmartStaff.API"));
    }
}
