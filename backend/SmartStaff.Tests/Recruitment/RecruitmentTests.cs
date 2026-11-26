using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartStaff.Application.Authentication;
using SmartStaff.Infrastructure.Persistence;
using SmartStaff.Tests.Authentication;

namespace SmartStaff.Tests.Recruitment;

[NonParallelizable]
public sealed class RecruitmentTests
{
    private const string Password = "Phase5-Test-Passw0rd!";
    private AuthApiFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void Start()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("POSTGRES_PASSWORD")))
        {
            Assert.Ignore("POSTGRES_PASSWORD is not set.");
        }

        _factory = new AuthApiFactory();
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
    public async Task Recruitment_ProfileApplicationAndPlacement_StayInsideAuthorizedRecords()
    {
        var admin = await CreateUserAsync("admin", AppRoles.Administrator);
        var manager = await CreateUserAsync("manager", AppRoles.FacilityManager);
        var outsider = await CreateUserAsync("outsider", AppRoles.FacilityManager);
        var recruiter = await CreateUserAsync("recruiter", AppRoles.Recruiter);
        var clinician = await CreateUserAsync("clinician", AppRoles.Clinician);
        var adminSession = await LoginAsync(admin.Email!);
        var managerSession = await LoginAsync(manager.Email!);
        var clinicianSession = await LoginAsync(clinician.Email!);

        var facility = await ReadAsync(await adminSession.SendAsync(HttpMethod.Post, "/api/facilities", new
        {
            name = "Phase Five Medical",
            facilityType = "Hospital",
            description = "Recruitment workflow campus",
            address = "50 Pipeline Road",
            city = "Austin",
            state = "TX",
            zipCode = "78704",
            contactEmail = "ops@phasefive.test",
            contactPhone = "555-0199",
            status = "Active"
        }, bearer: true));
        var facilityId = facility.GetProperty("id").GetGuid();
        var locationId = (await ReadAsync(await adminSession.SendAsync(HttpMethod.Post, $"/api/facilities/{facilityId}/locations", new
        {
            locationName = "North Wing",
            address = "50 Pipeline Road",
            city = "Austin",
            state = "TX",
            zipCode = "78704",
            isActive = true
        }, bearer: true))).GetProperty("id").GetGuid();
        Assert.That((await adminSession.SendAsync(HttpMethod.Post, $"/api/facilities/{facilityId}/members", new { userId = manager.Id, membershipRole = "Manager" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var requisitionId = (await ReadAsync(await managerSession.SendAsync(HttpMethod.Post, "/api/requisitions", new
        {
            facilityId,
            locationId,
            jobTitle = "ICU Nurse",
            description = "Published opening for the recruitment workflow.",
            specialty = "ICU",
            employmentType = "FullTime",
            shiftType = "Night",
            requiredExperience = 1,
            requiredCertifications = "RN",
            openPositions = 1,
            payRateMin = 40,
            payRateMax = 60,
            startDate = "2026-12-01"
        }, bearer: true))).GetProperty("id").GetGuid();
        Assert.That((await managerSession.SendAsync(HttpMethod.Patch, $"/api/requisitions/{requisitionId}/status", new { status = "Open" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await managerSession.SendAsync(HttpMethod.Patch, $"/api/requisitions/{requisitionId}/status", new { status = "Published" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await adminSession.SendAsync(HttpMethod.Patch, $"/api/requisitions/{requisitionId}/assign-recruiter", new { recruiterId = recruiter.Id }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        Assert.That((await clinicianSession.SendAsync(HttpMethod.Post, "/api/applications", new { jobRequisitionId = requisitionId }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        var invalidCredential = await clinicianSession.SendAsync(HttpMethod.Post, "/api/clinicians/me/credentials", new
        {
            credentialType = "License",
            credentialName = "RN",
            issuingOrganization = "Board",
            credentialNumber = "RN-1",
            issueDate = "2024-01-01",
            expirationDate = "2020-01-01"
        }, bearer: true);
        Assert.That(invalidCredential.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var saved = await clinicianSession.SendAsync(HttpMethod.Put, "/api/clinicians/me", new
        {
            firstName = "Casey",
            lastName = "Clinician",
            phoneNumber = "555-0111",
            professionalTitle = "Registered Nurse",
            specialty = "ICU",
            yearsOfExperience = 6,
            preferredLocations = "Austin",
            employmentPreference = "FullTime",
            availabilityDate = "2026-12-15"
        }, bearer: true);
        Assert.That(saved.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var profileId = (await ReadAsync(saved)).GetProperty("id").GetGuid();
        Assert.That((await clinicianSession.SendAsync(HttpMethod.Post, "/api/clinicians/me/credentials", new
        {
            credentialType = "License",
            credentialName = "Registered Nurse",
            issuingOrganization = "Texas Board",
            credentialNumber = "RN-100",
            issueDate = "2020-01-01",
            expirationDate = "2028-01-01"
        }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var applied = await clinicianSession.SendAsync(HttpMethod.Post, "/api/applications", new { jobRequisitionId = requisitionId }, bearer: true);
        Assert.That(applied.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var applicationId = (await ReadAsync(applied)).GetProperty("id").GetGuid();
        Assert.That((await clinicianSession.SendAsync(HttpMethod.Post, "/api/applications", new { jobRequisitionId = requisitionId }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That((await clinicianSession.SendAsync(HttpMethod.Patch, $"/api/applications/{applicationId}/status", new { status = "UnderReview", reason = "Nope" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Conflict));

        var recruiterSession = await LoginAsync(recruiter.Email!);
        foreach (var next in new[] { "UnderReview", "Screening", "SubmittedToFacility" })
        {
            Assert.That((await recruiterSession.SendAsync(HttpMethod.Patch, $"/api/applications/{applicationId}/status", new { status = next, reason = next, notes = "Pipeline" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        var outsiderSession = await LoginAsync(outsider.Email!);
        Assert.That((await outsiderSession.SendAsync(HttpMethod.Get, $"/api/applications/{applicationId}", bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That((await managerSession.SendAsync(HttpMethod.Patch, $"/api/applications/{applicationId}/status", new { status = "Interview", reason = "Schedule" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await managerSession.SendAsync(HttpMethod.Patch, $"/api/applications/{applicationId}/status", new { status = "Offer", reason = "Hire" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var placed = await recruiterSession.SendAsync(HttpMethod.Post, "/api/placements", new
        {
            jobApplicationId = applicationId,
            startDate = "2026-12-15",
            agreedPayRate = 55
        }, bearer: true);
        Assert.That(placed.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var placementId = (await ReadAsync(placed)).GetProperty("id").GetGuid();
        Assert.That((await recruiterSession.SendAsync(HttpMethod.Post, "/api/placements", new { jobApplicationId = applicationId, startDate = "2026-12-15", agreedPayRate = 55 }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That((await clinicianSession.SendAsync(HttpMethod.Get, $"/api/clinicians/{profileId}", bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await outsiderSession.SendAsync(HttpMethod.Get, $"/api/clinicians/{profileId}", bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var requisition = await db.JobRequisitions.FindAsync(requisitionId);
        var placement = await db.Placements.FindAsync(placementId);
        var history = await db.ApplicationStatusHistory.CountAsync(item => item.JobApplicationId == applicationId);
        Assert.That(requisition!.OpenPositions, Is.EqualTo(0));
        Assert.That(requisition.Status.ToString(), Is.EqualTo("Filled"));
        Assert.That(placement!.AgreedPayRate, Is.EqualTo(55m));
        Assert.That(history, Is.GreaterThanOrEqualTo(5));
    }

    private async Task<ApplicationUser> CreateUserAsync(string prefix, string role)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"phase5-{prefix}-{Guid.NewGuid():N}@smartstaff.test";
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FirstName = prefix, LastName = "Recruitment", IsActive = true };
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

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        Assert.That(response.StatusCode, Is.AnyOf(HttpStatusCode.OK, HttpStatusCode.Created), await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private sealed class Session(HttpClient client, string accessToken)
    {
        public Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body = null, bool bearer = false)
        {
            var request = new HttpRequestMessage(method, url);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body);
            }

            if (bearer)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }

            return client.SendAsync(request);
        }
    }
}
