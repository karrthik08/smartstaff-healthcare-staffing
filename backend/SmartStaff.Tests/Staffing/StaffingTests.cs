using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SmartStaff.Application.Authentication;
using SmartStaff.Infrastructure.Persistence;
using SmartStaff.Tests.Authentication;

namespace SmartStaff.Tests.Staffing;

[NonParallelizable]
public sealed class StaffingTests
{
    private const string Password = "Phase4-Test-Passw0rd!";
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
    public async Task Facility_CreateAndUpdate_PersistAndStayInsideAuthorizedFacilities()
    {
        var admin = await CreateUserAsync("admin", AppRoles.Administrator);
        var manager = await CreateUserAsync("manager", AppRoles.FacilityManager);
        var outsider = await CreateUserAsync("outsider", AppRoles.FacilityManager);
        var clinician = await CreateUserAsync("clinician", AppRoles.Clinician);
        var adminSession = await LoginAsync(admin.Email!);
        var clinicianSession = await LoginAsync(clinician.Email!);

        var denied = await clinicianSession.SendAsync(HttpMethod.Post, "/api/facilities", FacilityBody("Clinician Hospital"), bearer: true);
        Assert.That(denied.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        var created = await adminSession.SendAsync(HttpMethod.Post, "/api/facilities", FacilityBody("Lakeside Medical"), bearer: true);
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var facility = await created.Content.ReadFromJsonAsync<JsonElement>();
        var facilityId = facility.GetProperty("id").GetGuid();
        Assert.That(facility.GetProperty("name").GetString(), Is.EqualTo("Lakeside Medical"));

        var updatedBody = FacilityBody("Lakeside Medical Center");
        var updated = await adminSession.SendAsync(HttpMethod.Put, $"/api/facilities/{facilityId}", updatedBody, bearer: true);
        Assert.That(updated.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await updated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("name").GetString(), Is.EqualTo("Lakeside Medical Center"));

        var location = await adminSession.SendAsync(HttpMethod.Post, $"/api/facilities/{facilityId}/locations", new
        {
            locationName = "East Wing",
            address = "10 Care Lane",
            city = "Austin",
            state = "TX",
            zipCode = "78701",
            isActive = true
        }, bearer: true);
        Assert.That(location.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var locationId = (await location.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var membership = await adminSession.SendAsync(HttpMethod.Post, $"/api/facilities/{facilityId}/members", new
        {
            userId = manager.Id,
            membershipRole = "Manager"
        }, bearer: true);
        Assert.That(membership.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var outsiderSession = await LoginAsync(outsider.Email!);
        var cross = await outsiderSession.SendAsync(HttpMethod.Get, $"/api/facilities/{facilityId}", bearer: true);
        Assert.That(cross.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        var managerSession = await LoginAsync(manager.Email!);
        var visible = await managerSession.SendAsync(HttpMethod.Get, $"/api/facilities/{facilityId}", bearer: true);
        Assert.That(visible.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var invalid = await managerSession.SendAsync(HttpMethod.Post, "/api/requisitions", RequisitionBody(facilityId, locationId, 80, 40), bearer: true);
        Assert.That(invalid.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var requisitionResponse = await managerSession.SendAsync(HttpMethod.Post, "/api/requisitions", RequisitionBody(facilityId, locationId, 40, 55), bearer: true);
        Assert.That(requisitionResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var requisitionId = (await requisitionResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var skipped = await managerSession.SendAsync(HttpMethod.Patch, $"/api/requisitions/{requisitionId}/status", new { status = "Filled" }, bearer: true);
        Assert.That(skipped.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That((await adminSession.SendAsync(HttpMethod.Patch, $"/api/requisitions/{requisitionId}/status", new { status = "Open" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await adminSession.SendAsync(HttpMethod.Patch, $"/api/requisitions/{requisitionId}/status", new { status = "Published" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var recruiter = await CreateUserAsync("recruiter", AppRoles.Recruiter);
        var assigned = await adminSession.SendAsync(HttpMethod.Patch, $"/api/requisitions/{requisitionId}/assign-recruiter", new { recruiterId = recruiter.Id }, bearer: true);
        Assert.That(assigned.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var recruiterDenied = await (await LoginAsync(recruiter.Email!)).SendAsync(HttpMethod.Patch, $"/api/requisitions/{requisitionId}/assign-recruiter", new { recruiterId = recruiter.Id }, bearer: true);
        Assert.That(recruiterDenied.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        var recruiterList = await (await LoginAsync(recruiter.Email!)).SendAsync(HttpMethod.Get, "/api/requisitions?assignedToMe=true", bearer: true);
        var recruiterPage = await recruiterList.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(recruiterPage.GetProperty("items").EnumerateArray().Any(item => item.GetProperty("id").GetGuid() == requisitionId), Is.True);

        var clinicianList = await clinicianSession.SendAsync(HttpMethod.Get, "/api/requisitions?search=Registered%20Nurse&specialty=ICU&shiftType=Night&employmentType=FullTime", bearer: true);
        Assert.That(clinicianList.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var clinicianPage = await clinicianList.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(clinicianPage.GetProperty("items").EnumerateArray().Any(item => item.GetProperty("id").GetGuid() == requisitionId), Is.True);
        Assert.That((await clinicianSession.SendAsync(HttpMethod.Post, "/api/requisitions", RequisitionBody(facilityId, locationId, 40, 55), bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stored = await db.JobRequisitions.FindAsync(requisitionId);
        Assert.That(stored, Is.Not.Null);
        Assert.That(stored!.PayRateMin, Is.EqualTo(40m));
        Assert.That(stored.AssignedRecruiterId, Is.EqualTo(recruiter.Id));
        Assert.That(stored.Status.ToString(), Is.EqualTo("Published"));
    }

    private async Task<ApplicationUser> CreateUserAsync(string prefix, string role)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"phase4-{prefix}-{Guid.NewGuid():N}@smartstaff.test";
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = prefix,
            LastName = "Staffing",
            IsActive = true
        };
        var created = await users.CreateAsync(user, Password);
        Assert.That(created.Succeeded, Is.True, string.Join(" ", created.Errors.Select(error => error.Description)));
        Assert.That((await users.AddToRoleAsync(user, role)).Succeeded, Is.True);
        return user;
    }

    private async Task<Session> LoginAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new Session(_client, body.GetProperty("accessToken").GetString()!);
    }

    private static object FacilityBody(string name) => new
    {
        name,
        facilityType = "Hospital",
        description = "Acute care campus",
        address = "100 Health Ave",
        city = "Austin",
        state = "TX",
        zipCode = "78701",
        contactEmail = "ops@lakeside.test",
        contactPhone = "555-0100",
        status = "Active"
    };

    private static object RequisitionBody(Guid facilityId, Guid locationId, decimal minimum, decimal maximum) => new
    {
        facilityId,
        locationId,
        jobTitle = "Registered Nurse",
        description = "Night-shift ICU coverage for an open unit.",
        specialty = "ICU",
        employmentType = "FullTime",
        shiftType = "Night",
        requiredExperience = 2,
        requiredCertifications = "RN, BLS",
        openPositions = 2,
        payRateMin = minimum,
        payRateMax = maximum,
        startDate = "2026-12-01"
    };

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
