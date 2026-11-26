using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SmartStaff.Application.Authentication;
using SmartStaff.Infrastructure.Analytics;
using SmartStaff.Infrastructure.Persistence;
using SmartStaff.Tests.Authentication;

namespace SmartStaff.Tests.Analytics;

[NonParallelizable]
public sealed class AnalyticsTests
{
    private const string Password = "Phase9-Test-Passw0rd!";
    private AuthApiFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void Start()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("POSTGRES_PASSWORD")))
        {
            Assert.Ignore("POSTGRES_PASSWORD is not set.");
        }

        _factory = new AnalyticsApiFactory();
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
    }

    [OneTimeTearDown]
    public async Task Stop()
    {
        _client.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Test]
    public async Task Analytics_CalculatesScopedMetricsAndProtectsExports()
    {
        var admin = await CreateUserAsync("admin", AppRoles.Administrator);
        var manager = await CreateUserAsync("manager", AppRoles.FacilityManager);
        var otherManager = await CreateUserAsync("other", AppRoles.FacilityManager);
        var recruiter = await CreateUserAsync("recruiter", AppRoles.Recruiter);
        var otherRecruiter = await CreateUserAsync("other-recruiter", AppRoles.Recruiter);
        var clinician = await CreateUserAsync("clinician", AppRoles.Clinician);
        var empty = await CreateUserAsync("empty", AppRoles.Clinician);
        var adminSession = await LoginAsync(admin.Email!);
        var managerSession = await LoginAsync(manager.Email!);
        var facilityId = (await ReadAsync(await adminSession.SendAsync(HttpMethod.Post, "/api/facilities", new
        {
            name = "Metric Medical", facilityType = "Hospital", description = "Analytics campus", address = "9 Report Road", city = "Austin", state = "TX",
            zipCode = "78704", contactEmail = "ops@metric.test", contactPhone = "555-0199", status = "Active", timeZoneId = "America/Chicago"
        }, bearer: true))).GetProperty("id").GetGuid();
        var locationId = (await ReadAsync(await adminSession.SendAsync(HttpMethod.Post, $"/api/facilities/{facilityId}/locations", new
        {
            locationName = "Metric Wing", address = "9 Report Road", city = "Austin", state = "TX", zipCode = "78704", isActive = true
        }, bearer: true))).GetProperty("id").GetGuid();
        Assert.That((await adminSession.SendAsync(HttpMethod.Post, $"/api/facilities/{facilityId}/members", new { userId = manager.Id, membershipRole = "Manager" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var requisitionId = (await ReadAsync(await managerSession.SendAsync(HttpMethod.Post, "/api/requisitions", new
        {
            facilityId, locationId, jobTitle = "=SUM(1)", description = "Analytics coverage.",
            specialty = "ICU", employmentType = "FullTime", shiftType = "Day", requiredExperience = 1, requiredCertifications = "RN",
            openPositions = 1, payRateMin = 40, payRateMax = 60, startDate = "2026-12-01"
        }, bearer: true))).GetProperty("id").GetGuid();
        Assert.That((await managerSession.SendAsync(HttpMethod.Patch, $"/api/requisitions/{requisitionId}/status", new { status = "Open" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await managerSession.SendAsync(HttpMethod.Patch, $"/api/requisitions/{requisitionId}/status", new { status = "Published" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await adminSession.SendAsync(HttpMethod.Patch, $"/api/requisitions/{requisitionId}/assign-recruiter", new { recruiterId = recruiter.Id }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var clinicianSession = await LoginAsync(clinician.Email!);
        await ReadAsync(await clinicianSession.SendAsync(HttpMethod.Put, "/api/clinicians/me", new
        {
            firstName = "Casey", lastName = "MetricSecret", phoneNumber = "555-0199", professionalTitle = "Registered Nurse",
            specialty = "ICU", yearsOfExperience = 3, preferredLocations = "Austin", employmentPreference = "FullTime", availabilityDate = "2026-12-01"
        }, bearer: true));
        var applicationId = (await ReadAsync(await clinicianSession.SendAsync(HttpMethod.Post, "/api/applications", new { jobRequisitionId = requisitionId }, bearer: true))).GetProperty("id").GetGuid();
        Assert.That((await adminSession.SendAsync(HttpMethod.Patch, $"/api/applications/{applicationId}/status", new { status = "UnderReview", reason = "Review" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await adminSession.SendAsync(HttpMethod.Patch, $"/api/applications/{applicationId}/status", new { status = "Screening", reason = "Screen" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await adminSession.SendAsync(HttpMethod.Patch, $"/api/applications/{applicationId}/status", new { status = "SubmittedToFacility", reason = "Submit" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await managerSession.SendAsync(HttpMethod.Patch, $"/api/applications/{applicationId}/status", new { status = "Interview", reason = "Interview" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await managerSession.SendAsync(HttpMethod.Patch, $"/api/applications/{applicationId}/status", new { status = "Offer", reason = "Offer" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await managerSession.SendAsync(HttpMethod.Post, "/api/placements", new { jobApplicationId = applicationId, startDate = "2026-12-01", agreedPayRate = 52 }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That((await clinicianSession.SendAsync(HttpMethod.Post, "/api/clinicians/me/credentials", new
        {
            credentialType = "License", credentialName = "RN", issuingOrganization = "Board", credentialNumber = "SECRET-999",
            issueDate = "2026-01-01", expirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)).ToString("yyyy-MM-dd")
        }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var shiftId = (await ReadAsync(await managerSession.SendAsync(HttpMethod.Post, "/api/shifts", new
        {
            facilityId, facilityLocationId = locationId, jobRequisitionId = requisitionId, startLocal = "2027-02-02T07:00:00", endLocal = "2027-02-02T15:00:00", requiredClinicians = 1, specialty = "ICU"
        }, bearer: true))).GetProperty("id").GetGuid();
        Assert.That((await managerSession.SendAsync(HttpMethod.Patch, $"/api/shifts/{shiftId}/publish", bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        await ReadAsync(await clinicianSession.SendAsync(HttpMethod.Post, "/api/timesheets", new { facilityId, periodStart = "2027-02-01", periodEnd = "2027-02-07" }, bearer: true));

        var recruitment = await ReadAsync(await adminSession.SendAsync(HttpMethod.Get, $"/api/analytics/recruitment?facilityId={facilityId}", bearer: true), HttpStatusCode.OK);
        Assert.That(recruitment.GetProperty("publishedPositions").GetInt32(), Is.EqualTo(0));
        Assert.That(recruitment.GetProperty("openRequisitions").GetInt32(), Is.EqualTo(0));
        Assert.That(recruitment.GetProperty("filledPositions").GetInt32(), Is.EqualTo(1));
        Assert.That(recruitment.GetProperty("placements").GetInt32(), Is.EqualTo(1));
        Assert.That(recruitment.GetProperty("activeApplications").GetInt32(), Is.EqualTo(0));
        Assert.That(recruitment.GetProperty("placementConversionPercent").GetDecimal(), Is.EqualTo(100));
        Assert.That(recruitment.GetProperty("applicationsByStatus").EnumerateArray().Any(item => item.GetProperty("label").GetString() == "Placed" && item.GetProperty("value").GetDecimal() == 1), Is.True);
        var emptyRange = await ReadAsync(await adminSession.SendAsync(HttpMethod.Get, $"/api/analytics/recruitment?facilityId={facilityId}&from=1999-01-01&to=1999-01-02", bearer: true), HttpStatusCode.OK);
        Assert.That(emptyRange.GetProperty("publishedPositions").GetInt32(), Is.EqualTo(0));
        Assert.That(emptyRange.GetProperty("activeApplications").GetInt32(), Is.EqualTo(0));

        var managerView = await ReadAsync(await managerSession.SendAsync(HttpMethod.Get, "/api/analytics/overview", bearer: true), HttpStatusCode.OK);
        Assert.That(managerView.GetProperty("filledPositions").GetInt32(), Is.EqualTo(1));
        Assert.That((await (await LoginAsync(otherManager.Email!)).SendAsync(HttpMethod.Get, $"/api/analytics/overview?facilityId={facilityId}", bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var recruiterView = await ReadAsync(await (await LoginAsync(recruiter.Email!)).SendAsync(HttpMethod.Get, "/api/analytics/recruitment", bearer: true), HttpStatusCode.OK);
        Assert.That(recruiterView.GetProperty("filledPositions").GetInt32(), Is.EqualTo(1));
        var otherRecruiterView = await ReadAsync(await (await LoginAsync(otherRecruiter.Email!)).SendAsync(HttpMethod.Get, "/api/analytics/recruitment", bearer: true), HttpStatusCode.OK);
        Assert.That(otherRecruiterView.GetProperty("filledPositions").GetInt32(), Is.EqualTo(0));

        var personal = await ReadAsync(await clinicianSession.SendAsync(HttpMethod.Get, "/api/analytics/recruitment", bearer: true), HttpStatusCode.OK);
        Assert.That(personal.GetProperty("filledPositions").GetInt32(), Is.EqualTo(1));
        Assert.That(personal.GetProperty("placementConversionPercent").GetDecimal(), Is.EqualTo(100));
        var scheduling = await ReadAsync(await managerSession.SendAsync(HttpMethod.Get, "/api/analytics/scheduling", bearer: true), HttpStatusCode.OK);
        Assert.That(scheduling.GetProperty("scheduledShifts").GetInt32(), Is.EqualTo(1));
        Assert.That(scheduling.GetProperty("unfilledShifts").GetInt32(), Is.EqualTo(1));
        Assert.That(scheduling.GetProperty("confirmedAssignments").GetInt32(), Is.EqualTo(0));
        var timesheets = await ReadAsync(await managerSession.SendAsync(HttpMethod.Get, "/api/analytics/timesheets", bearer: true), HttpStatusCode.OK);
        Assert.That(timesheets.GetProperty("draft").GetInt32(), Is.EqualTo(1));
        Assert.That(timesheets.GetProperty("approved").GetInt32(), Is.EqualTo(0));
        var credentials = await ReadAsync(await clinicianSession.SendAsync(HttpMethod.Get, "/api/analytics/credentials", bearer: true), HttpStatusCode.OK);
        Assert.That(credentials.GetProperty("pendingVerification").GetInt32(), Is.EqualTo(1));
        Assert.That(credentials.GetProperty("expiring").GetInt32(), Is.EqualTo(1));
        var emptyOverview = await ReadAsync(await (await LoginAsync(empty.Email!)).SendAsync(HttpMethod.Get, "/api/analytics/overview", bearer: true), HttpStatusCode.OK);
        Assert.That(emptyOverview.GetProperty("openRequisitions").GetInt32(), Is.EqualTo(0));
        Assert.That(emptyOverview.GetProperty("scheduledShifts").GetInt32(), Is.EqualTo(0));

        var export = await adminSession.SendAsync(HttpMethod.Get, $"/api/analytics/export?report=recruitment&facilityId={facilityId}&pageSize=50", bearer: true);
        Assert.That(export.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var csv = await export.Content.ReadAsStringAsync();
        Assert.That(csv, Does.Contain("'=SUM(1)"));
        Assert.That(csv, Does.Not.Contain("SECRET-999"));
        Assert.That(csv, Does.Not.Contain("MetricSecret"));
        var credentialExport = await clinicianSession.SendAsync(HttpMethod.Get, "/api/analytics/export?report=credentials&pageSize=50", bearer: true);
        var credentialCsv = await credentialExport.Content.ReadAsStringAsync();
        Assert.That(credentialCsv, Does.Not.Contain("SECRET-999"));
        Assert.That((await clinicianSession.SendAsync(HttpMethod.Get, $"/api/analytics/overview?facilityId={Guid.NewGuid()}", bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public void CsvSafety_PrefixesFormulaCharacters()
    {
        var csv = Encoding.UTF8.GetString(CsvSafety.Build([new[] { "=cmd", "+1", "safe" }]));
        Assert.That(csv, Does.Contain("'=cmd"));
        Assert.That(csv, Does.Contain("'+1"));
        Assert.That(csv, Does.Contain("safe"));
    }

    private async Task<ApplicationUser> CreateUserAsync(string prefix, string role)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"phase9-{prefix}-{Guid.NewGuid():N}@smartstaff.test";
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FirstName = prefix, LastName = "Analytics", IsActive = true };
        Assert.That((await users.CreateAsync(user, Password)).Succeeded, Is.True);
        Assert.That((await users.AddToRoleAsync(user, role)).Succeeded, Is.True);
        return user;
    }

    private async Task<Session> LoginAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return new Session(_client, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.Created)
    {
        var allowed = expected == HttpStatusCode.Created ? new[] { HttpStatusCode.OK, HttpStatusCode.Created } : new[] { expected };
        Assert.That(allowed, Does.Contain(response.StatusCode), await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private sealed class Session(HttpClient client, string accessToken)
    {
        public Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body = null, bool bearer = false)
        {
            var request = new HttpRequestMessage(method, url) { Content = body is null ? null : JsonContent.Create(body) };
            if (bearer)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }

            return client.SendAsync(request);
        }
    }

    private sealed class AnalyticsApiFactory : AuthApiFactory
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseSetting("NOTIFICATION_POLL_SECONDS", "3600");
            base.ConfigureWebHost(builder);
        }
    }
}
