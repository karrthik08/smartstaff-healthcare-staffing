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

namespace SmartStaff.Tests.Scheduling;

[NonParallelizable]
public sealed class SchedulingTests
{
    private const string Password = "Phase6-Test-Passw0rd!";
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
    public async Task Scheduling_EnforcesPlacementCapacityTimezoneAndAuthorization()
    {
        var admin = await CreateUserAsync("admin", AppRoles.Administrator);
        var manager = await CreateUserAsync("manager", AppRoles.FacilityManager);
        var outsider = await CreateUserAsync("outsider", AppRoles.FacilityManager);
        var recruiter = await CreateUserAsync("recruiter", AppRoles.Recruiter);
        var first = await CreateUserAsync("one", AppRoles.Clinician);
        var second = await CreateUserAsync("two", AppRoles.Clinician);
        var adminSession = await LoginAsync(admin.Email!);
        var managerSession = await LoginAsync(manager.Email!);
        var facility = await ReadAsync(await adminSession.SendAsync(HttpMethod.Post, "/api/facilities", FacilityBody("Schedule Medical", "America/New_York"), bearer: true));
        var facilityId = facility.GetProperty("id").GetGuid();
        var locationId = (await ReadAsync(await adminSession.SendAsync(HttpMethod.Post, $"/api/facilities/{facilityId}/locations", new
        {
            locationName = "Night Wing", address = "9 Clock Road", city = "Austin", state = "TX", zipCode = "78704", isActive = true
        }, bearer: true))).GetProperty("id").GetGuid();
        Assert.That((await adminSession.SendAsync(HttpMethod.Post, $"/api/facilities/{facilityId}/members", new { userId = manager.Id, membershipRole = "Manager" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var requisitionId = await PublishRequisitionAsync(managerSession, adminSession, facilityId, locationId, recruiter.Id!);
        var firstProfile = await PrepareClinicianAsync(first, requisitionId, managerSession);
        var secondProfile = await PrepareClinicianAsync(second, requisitionId, managerSession);
        var recruiterSession = await LoginAsync(recruiter.Email!);
        await PlaceAsync(recruiterSession, firstProfile.ApplicationId);
        await PlaceAsync(recruiterSession, secondProfile.ApplicationId);

        var invalid = await managerSession.SendAsync(HttpMethod.Post, "/api/shifts", ShiftBody(facilityId, locationId, requisitionId, "2026-03-08T02:30:00", "2026-03-08T10:00:00"), bearer: true);
        Assert.That(invalid.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var ambiguous = await managerSession.SendAsync(HttpMethod.Post, "/api/shifts", ShiftBody(facilityId, locationId, requisitionId, "2026-11-01T01:30:00", "2026-11-01T09:00:00"), bearer: true);
        Assert.That(ambiguous.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await adminSession.SendAsync(HttpMethod.Put, $"/api/facilities/{facilityId}", FacilityBody("Schedule Medical", "America/Chicago"), bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var backwards = await managerSession.SendAsync(HttpMethod.Post, "/api/shifts", ShiftBody(facilityId, locationId, requisitionId, "2026-12-16T15:00:00", "2026-12-16T07:00:00"), bearer: true);
        Assert.That(backwards.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await (await LoginAsync(outsider.Email!)).SendAsync(HttpMethod.Post, "/api/shifts", ShiftBody(facilityId, locationId, requisitionId, "2026-12-16T22:00:00", "2026-12-17T06:00:00"), bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        var created = await ReadAsync(await managerSession.SendAsync(HttpMethod.Post, "/api/shifts", ShiftBody(facilityId, locationId, requisitionId, "2026-12-16T22:00:00", "2026-12-17T06:00:00"), bearer: true));
        var shiftId = created.GetProperty("id").GetGuid();
        Assert.That(created.GetProperty("startLocal").GetString(), Is.EqualTo("2026-12-16T22:00"));
        Assert.That(created.GetProperty("endDateTime").GetDateTimeOffset() - created.GetProperty("startDateTime").GetDateTimeOffset(), Is.EqualTo(TimeSpan.FromHours(8)));
        Assert.That((await managerSession.SendAsync(HttpMethod.Patch, $"/api/shifts/{shiftId}/publish", bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var firstSession = await LoginAsync(first.Email!);
        Assert.That((await firstSession.SendAsync(HttpMethod.Get, $"/api/shifts/{shiftId}", bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That((await firstSession.SendAsync(HttpMethod.Post, $"/api/shifts/{shiftId}/assignments", new { clinicianProfileId = firstProfile.ProfileId }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        var assigned = await ReadAsync(await managerSession.SendAsync(HttpMethod.Post, $"/api/shifts/{shiftId}/assignments", new { clinicianProfileId = firstProfile.ProfileId }, bearer: true), HttpStatusCode.Created);
        var assignmentId = assigned.GetProperty("id").GetGuid();
        Assert.That((await managerSession.SendAsync(HttpMethod.Post, $"/api/shifts/{shiftId}/assignments", new { clinicianProfileId = secondProfile.ProfileId }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        var confirmed = await ReadAsync(await firstSession.SendAsync(HttpMethod.Patch, $"/api/shifts/assignments/{assignmentId}/confirm", bearer: true));
        Assert.That(confirmed.GetProperty("assignmentStatus").GetString(), Is.EqualTo("Confirmed"));
        Assert.That((await (await LoginAsync(second.Email!)).SendAsync(HttpMethod.Patch, $"/api/shifts/assignments/{assignmentId}/confirm", bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var visible = await ReadAsync(await managerSession.SendAsync(HttpMethod.Get, "/api/shifts?view=filled", bearer: true));
        Assert.That(visible.GetProperty("items").EnumerateArray().Any(item => item.GetProperty("id").GetGuid() == shiftId && item.GetProperty("assignedClinicians").GetInt32() == 1), Is.True);

        var overlap = await ReadAsync(await managerSession.SendAsync(HttpMethod.Post, "/api/shifts", ShiftBody(facilityId, locationId, requisitionId, "2026-12-17T01:00:00", "2026-12-17T09:00:00", 1), bearer: true));
        Assert.That((await managerSession.SendAsync(HttpMethod.Post, $"/api/shifts/{overlap.GetProperty("id").GetGuid()}/assignments", new { clinicianProfileId = firstProfile.ProfileId }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Conflict));

        var raceShift = await ReadAsync(await managerSession.SendAsync(HttpMethod.Post, "/api/shifts", ShiftBody(facilityId, locationId, requisitionId, "2026-12-20T07:00:00", "2026-12-20T15:00:00", 1), bearer: true));
        var raceId = raceShift.GetProperty("id").GetGuid();
        var raced = await Task.WhenAll(
            managerSession.SendAsync(HttpMethod.Post, $"/api/shifts/{raceId}/assignments", new { clinicianProfileId = firstProfile.ProfileId }, bearer: true),
            managerSession.SendAsync(HttpMethod.Post, $"/api/shifts/{raceId}/assignments", new { clinicianProfileId = secondProfile.ProfileId }, bearer: true));
        Assert.That(raced.Count(response => response.StatusCode == HttpStatusCode.Created), Is.EqualTo(1));
        Assert.That(raced.Count(response => response.StatusCode == HttpStatusCode.Conflict), Is.EqualTo(1));

        var availability = await firstSession.SendAsync(HttpMethod.Post, "/api/availability", new
        {
            availableFrom = "2026-12-16T04:00:00Z",
            availableUntil = "2026-12-17T12:00:00Z",
            availabilityType = "Available",
            notes = "Nights"
        }, bearer: true);
        Assert.That(availability.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var change = await ReadAsync(await firstSession.SendAsync(HttpMethod.Post, "/api/shift-changes", new { shiftAssignmentId = assignmentId, requestType = "Cancel", reason = "Need another night" }, bearer: true), HttpStatusCode.Created);
        Assert.That((await (await LoginAsync(outsider.Email!)).SendAsync(HttpMethod.Patch, $"/api/shift-changes/{change.GetProperty("id").GetGuid()}", new { status = "Approved" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.That(await db.Shifts.CountAsync(item => item.FacilityId == facilityId), Is.EqualTo(3));
        Assert.That(await db.ShiftAssignments.CountAsync(item => item.AssignmentStatus == Domain.Scheduling.AssignmentStatus.Confirmed), Is.GreaterThanOrEqualTo(1));
    }

    private async Task<Guid> PublishRequisitionAsync(Session manager, Session admin, Guid facilityId, Guid locationId, string recruiterId)
    {
        var requisitionId = (await ReadAsync(await manager.SendAsync(HttpMethod.Post, "/api/requisitions", new
        {
            facilityId, locationId, jobTitle = "Night Nurse", description = "Overnight coverage for the scheduling workflow.",
            specialty = "ICU", employmentType = "FullTime", shiftType = "Night", requiredExperience = 1, requiredCertifications = "RN",
            openPositions = 2, payRateMin = 40, payRateMax = 60, startDate = "2026-12-01"
        }, bearer: true))).GetProperty("id").GetGuid();
        Assert.That((await manager.SendAsync(HttpMethod.Patch, $"/api/requisitions/{requisitionId}/status", new { status = "Open" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await manager.SendAsync(HttpMethod.Patch, $"/api/requisitions/{requisitionId}/status", new { status = "Published" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await admin.SendAsync(HttpMethod.Patch, $"/api/requisitions/{requisitionId}/assign-recruiter", new { recruiterId }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return requisitionId;
    }

    private async Task<(Guid ProfileId, Guid ApplicationId)> PrepareClinicianAsync(ApplicationUser clinician, Guid requisitionId, Session manager)
    {
        var session = await LoginAsync(clinician.Email!);
        var profile = await ReadAsync(await session.SendAsync(HttpMethod.Put, "/api/clinicians/me", new
        {
            firstName = clinician.FirstName, lastName = "Scheduling", phoneNumber = "555-0170", professionalTitle = "Registered Nurse",
            specialty = "ICU", yearsOfExperience = 4, preferredLocations = "Austin", employmentPreference = "FullTime", availabilityDate = "2026-12-01"
        }, bearer: true));
        var application = await ReadAsync(await session.SendAsync(HttpMethod.Post, "/api/applications", new { jobRequisitionId = requisitionId }, bearer: true), HttpStatusCode.Created);
        var applicationId = application.GetProperty("id").GetGuid();
        foreach (var status in new[] { "UnderReview", "Screening", "SubmittedToFacility" })
        {
            Assert.That((await (await LoginAsync((await RecruiterEmailAsync(requisitionId)))).SendAsync(HttpMethod.Patch, $"/api/applications/{applicationId}/status", new { status, reason = status }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        Assert.That((await manager.SendAsync(HttpMethod.Patch, $"/api/applications/{applicationId}/status", new { status = "Interview", reason = "Interview" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await manager.SendAsync(HttpMethod.Patch, $"/api/applications/{applicationId}/status", new { status = "Offer", reason = "Offer" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return (profile.GetProperty("id").GetGuid(), applicationId);
    }

    private async Task<string> RecruiterEmailAsync(Guid requisitionId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var recruiterId = (await db.JobRequisitions.FindAsync(requisitionId))!.AssignedRecruiterId!;
        return (await users.FindByIdAsync(recruiterId))!.Email!;
    }

    private static async Task PlaceAsync(Session recruiter, Guid applicationId)
    {
        Assert.That((await recruiter.SendAsync(HttpMethod.Post, "/api/placements", new { jobApplicationId = applicationId, startDate = "2026-12-01", agreedPayRate = 52 }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    private async Task<ApplicationUser> CreateUserAsync(string prefix, string role)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"phase6-{prefix}-{Guid.NewGuid():N}@smartstaff.test";
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FirstName = prefix, LastName = "Scheduling", IsActive = true };
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

    private static object FacilityBody(string name, string timeZoneId) => new
    {
        name, facilityType = "Hospital", description = "Scheduling campus", address = "9 Clock Road", city = "Austin", state = "TX",
        zipCode = "78704", contactEmail = "ops@schedule.test", contactPhone = "555-0160", status = "Active", timeZoneId
    };

    private static object ShiftBody(Guid facilityId, Guid locationId, Guid requisitionId, string start, string end, int required = 1) => new
    {
        facilityId, facilityLocationId = locationId, jobRequisitionId = requisitionId, startLocal = start, endLocal = end, requiredClinicians = required, specialty = "ICU"
    };

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
}
