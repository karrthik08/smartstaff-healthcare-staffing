using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartStaff.Application.Authentication;
using SmartStaff.Domain.Timekeeping;
using SmartStaff.Infrastructure.Persistence;
using SmartStaff.Tests.Authentication;

namespace SmartStaff.Tests.Timekeeping;

[NonParallelizable]
public sealed class TimekeepingTests
{
    private const string Password = "Phase7-Test-Passw0rd!";
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
    public async Task Timekeeping_EnforcesAttendanceHoursApprovalAndFacilityIsolation()
    {
        var admin = await CreateUserAsync("admin", AppRoles.Administrator);
        var manager = await CreateUserAsync("manager", AppRoles.FacilityManager);
        var outsider = await CreateUserAsync("outsider", AppRoles.FacilityManager);
        var recruiter = await CreateUserAsync("recruiter", AppRoles.Recruiter);
        var clinician = await CreateUserAsync("clinician", AppRoles.Clinician);
        var other = await CreateUserAsync("other", AppRoles.Clinician);
        var adminSession = await LoginAsync(admin.Email!);
        var managerSession = await LoginAsync(manager.Email!);
        var facility = await ReadAsync(await adminSession.SendAsync(HttpMethod.Post, "/api/facilities", FacilityBody(), bearer: true));
        var facilityId = facility.GetProperty("id").GetGuid();
        var locationId = (await ReadAsync(await adminSession.SendAsync(HttpMethod.Post, $"/api/facilities/{facilityId}/locations", new
        {
            locationName = "Timesheet Wing", address = "9 Clock Road", city = "Austin", state = "TX", zipCode = "78704", isActive = true
        }, bearer: true))).GetProperty("id").GetGuid();
        Assert.That((await adminSession.SendAsync(HttpMethod.Post, $"/api/facilities/{facilityId}/members", new { userId = manager.Id, membershipRole = "Manager" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var requisitionId = await PublishRequisitionAsync(managerSession, adminSession, facilityId, locationId, recruiter.Id!);
        var first = await PrepareClinicianAsync(clinician, requisitionId, managerSession);
        var second = await PrepareClinicianAsync(other, requisitionId, managerSession);
        var recruiterSession = await LoginAsync(recruiter.Email!);
        await PlaceAsync(recruiterSession, first.ApplicationId);
        await PlaceAsync(recruiterSession, second.ApplicationId);
        var clinicianSession = await LoginAsync(clinician.Email!);
        var otherSession = await LoginAsync(other.Email!);
        var outsiderSession = await LoginAsync(outsider.Email!);

        var firstShift = await PublishShiftAsync(managerSession, facilityId, locationId, requisitionId, "2026-12-16T07:00:00", "2026-12-16T17:00:00");
        var secondShift = await PublishShiftAsync(managerSession, facilityId, locationId, requisitionId, "2026-12-18T07:00:00", "2026-12-18T17:00:00");
        var firstAssignment = await AssignAsync(managerSession, clinicianSession, firstShift, first.ProfileId);
        var secondAssignment = await AssignAsync(managerSession, clinicianSession, secondShift, first.ProfileId);

        Assert.That((await clinicianSession.SendAsync(HttpMethod.Post, $"/api/attendance/{firstAssignment}/clock-in", new { local = "2026-11-01T01:30:00" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await clinicianSession.SendAsync(HttpMethod.Post, $"/api/attendance/{firstAssignment}/clock-in", new { local = "2026-03-08T02:30:00" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await otherSession.SendAsync(HttpMethod.Post, $"/api/attendance/{firstAssignment}/clock-in", new { local = "2026-12-16T07:00:00" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var clockIn = await ReadAsync(await clinicianSession.SendAsync(HttpMethod.Post, $"/api/attendance/{firstAssignment}/clock-in", new { local = "2026-12-16T07:00:00" }, bearer: true), HttpStatusCode.Created);
        Assert.That(clockIn.GetProperty("clockInTime").GetDateTimeOffset().UtcDateTime, Is.EqualTo(new DateTime(2026, 12, 16, 13, 0, 0, DateTimeKind.Utc)));
        var repeated = await ReadAsync(await clinicianSession.SendAsync(HttpMethod.Post, $"/api/attendance/{firstAssignment}/clock-in", new { at = "2026-12-16T13:00:00Z" }, bearer: true), HttpStatusCode.OK);
        Assert.That(repeated.GetProperty("id").GetGuid(), Is.EqualTo(clockIn.GetProperty("id").GetGuid()));
        Assert.That((await clinicianSession.SendAsync(HttpMethod.Post, $"/api/attendance/{firstAssignment}/clock-out", new { at = "2026-12-16T12:00:00Z" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await clinicianSession.SendAsync(HttpMethod.Post, $"/api/attendance/{secondAssignment}/clock-out", new { at = "2026-12-18T23:00:00Z" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        var clockOut = await ReadAsync(await clinicianSession.SendAsync(HttpMethod.Post, $"/api/attendance/{firstAssignment}/clock-out", new { at = "2026-12-16T23:00:00Z" }, bearer: true));
        Assert.That(clockOut.GetProperty("attendanceStatus").GetString(), Is.EqualTo("Completed"));
        Assert.That((await clinicianSession.SendAsync(HttpMethod.Post, $"/api/attendance/{firstAssignment}/clock-in", new { at = "2026-12-16T13:00:00Z" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That((await clinicianSession.SendAsync(HttpMethod.Post, $"/api/attendance/{secondAssignment}/clock-in", new { at = "2026-12-16T20:00:00Z" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That((await clinicianSession.SendAsync(HttpMethod.Post, $"/api/attendance/{secondAssignment}/clock-in", new { at = "2026-12-18T13:00:00Z" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That((await clinicianSession.SendAsync(HttpMethod.Post, $"/api/attendance/{secondAssignment}/clock-out", new { at = "2026-12-20T01:00:00Z" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var timesheet = await ReadAsync(await clinicianSession.SendAsync(HttpMethod.Post, "/api/timesheets", new { facilityId, periodStart = "2026-12-14", periodEnd = "2026-12-17" }, bearer: true), HttpStatusCode.Created);
        var timesheetId = timesheet.GetProperty("id").GetGuid();
        Assert.That((await clinicianSession.SendAsync(HttpMethod.Post, $"/api/timesheets/{timesheetId}/entries", Entry(firstAssignment, "2026-12-16T13:00:00Z", "2026-12-16T23:00:00Z", 600), bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var withHours = await ReadAsync(await clinicianSession.SendAsync(HttpMethod.Post, $"/api/timesheets/{timesheetId}/entries", Entry(firstAssignment, "2026-12-16T13:00:00Z", "2026-12-16T23:00:00Z", 0), bearer: true));
        Assert.That(withHours.GetProperty("totalRegularHours").GetDecimal(), Is.EqualTo(10m));
        Assert.That(withHours.GetProperty("totalOvertimeHours").GetDecimal(), Is.EqualTo(0m));
        Assert.That((await clinicianSession.SendAsync(HttpMethod.Post, $"/api/timesheets/{timesheetId}/entries", Entry(firstAssignment, "2026-12-16T13:00:00Z", "2026-12-16T23:00:00Z", 0), bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That((await managerSession.SendAsync(HttpMethod.Post, $"/api/timesheets/{timesheetId}/approve", bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        var submitted = await ReadAsync(await clinicianSession.SendAsync(HttpMethod.Post, $"/api/timesheets/{timesheetId}/submit", bearer: true));
        Assert.That(submitted.GetProperty("status").GetString(), Is.EqualTo("Submitted"));
        Assert.That((await clinicianSession.SendAsync(HttpMethod.Post, $"/api/timesheets/{timesheetId}/approve", bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That((await recruiterSession.SendAsync(HttpMethod.Post, $"/api/timesheets/{timesheetId}/approve", bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That((await outsiderSession.SendAsync(HttpMethod.Post, $"/api/timesheets/{timesheetId}/approve", bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That((await outsiderSession.SendAsync(HttpMethod.Get, $"/api/timesheets/{timesheetId}", bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That((await managerSession.SendAsync(HttpMethod.Post, $"/api/timesheets/{timesheetId}/reject", new { reason = "" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var raced = await Task.WhenAll(
            managerSession.SendAsync(HttpMethod.Post, $"/api/timesheets/{timesheetId}/approve", bearer: true),
            managerSession.SendAsync(HttpMethod.Post, $"/api/timesheets/{timesheetId}/approve", bearer: true));
        Assert.That(raced.All(response => response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict), Is.True);
        Assert.That(raced.Any(response => response.StatusCode == HttpStatusCode.OK), Is.True);
        var approved = await ReadAsync(await managerSession.SendAsync(HttpMethod.Get, $"/api/timesheets/{timesheetId}", bearer: true), HttpStatusCode.OK);
        Assert.That(approved.GetProperty("status").GetString(), Is.EqualTo("Approved"));
        Assert.That((await clinicianSession.SendAsync(HttpMethod.Post, $"/api/timesheets/{timesheetId}/entries", Entry(secondAssignment, "2026-12-18T13:00:00Z", "2026-12-18T23:00:00Z", 0), bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        var history = await ReadAsync(await managerSession.SendAsync(HttpMethod.Get, $"/api/timesheets/{timesheetId}/history", bearer: true), HttpStatusCode.OK);
        Assert.That(history.EnumerateArray().Count(item => item.GetProperty("newStatus").GetString() == "Approved"), Is.EqualTo(1));

        var corrected = await ReadAsync(await clinicianSession.SendAsync(HttpMethod.Post, "/api/timesheets", new { facilityId, periodStart = "2026-12-18", periodEnd = "2026-12-21" }, bearer: true), HttpStatusCode.Created);
        var correctedId = corrected.GetProperty("id").GetGuid();
        var acrossWeeks = await ReadAsync(await clinicianSession.SendAsync(HttpMethod.Post, $"/api/timesheets/{correctedId}/entries", Entry(secondAssignment, "2026-12-18T13:00:00Z", "2026-12-20T01:00:00Z", 0), bearer: true));
        Assert.That(acrossWeeks.GetProperty("totalRegularHours").GetDecimal(), Is.EqualTo(30m));
        Assert.That(acrossWeeks.GetProperty("totalOvertimeHours").GetDecimal(), Is.EqualTo(6m));
        var preserved = await ReadAsync(await clinicianSession.SendAsync(HttpMethod.Get, $"/api/timesheets/{timesheetId}", bearer: true), HttpStatusCode.OK);
        Assert.That(preserved.GetProperty("totalRegularHours").GetDecimal(), Is.EqualTo(10m));
        Assert.That(preserved.GetProperty("totalOvertimeHours").GetDecimal(), Is.EqualTo(0m));
        Assert.That((await adminSession.SendAsync(HttpMethod.Post, "/api/overtime-policies", new { jurisdictionCode = "TX", dailyOvertimeThreshold = 8, weeklyOvertimeThreshold = 40, workweekStartsOn = "Sunday" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var daily = await ReadAsync(await clinicianSession.SendAsync(HttpMethod.Put, $"/api/timesheets/{correctedId}/entries/{acrossWeeks.GetProperty("entries")[0].GetProperty("id").GetGuid()}", Entry(secondAssignment, "2026-12-18T13:00:00Z", "2026-12-18T23:00:00Z", 0), bearer: true));
        Assert.That(daily.GetProperty("totalRegularHours").GetDecimal(), Is.EqualTo(8m));
        Assert.That(daily.GetProperty("totalOvertimeHours").GetDecimal(), Is.EqualTo(2m));
        preserved = await ReadAsync(await clinicianSession.SendAsync(HttpMethod.Get, $"/api/timesheets/{timesheetId}", bearer: true), HttpStatusCode.OK);
        Assert.That(preserved.GetProperty("totalRegularHours").GetDecimal(), Is.EqualTo(10m));
        var jurisdiction = daily;
        await ReadAsync(await clinicianSession.SendAsync(HttpMethod.Post, $"/api/timesheets/{correctedId}/submit", bearer: true));
        var rejected = await ReadAsync(await managerSession.SendAsync(HttpMethod.Post, $"/api/timesheets/{correctedId}/reject", new { reason = "Break was missing" }, bearer: true));
        Assert.That(rejected.GetProperty("status").GetString(), Is.EqualTo("Rejected"));
        var entryId = jurisdiction.GetProperty("entries")[0].GetProperty("id").GetGuid();
        var redrafted = await ReadAsync(await clinicianSession.SendAsync(HttpMethod.Put, $"/api/timesheets/{correctedId}/entries/{entryId}", Entry(secondAssignment, "2026-12-18T13:00:00Z", "2026-12-18T21:30:00Z", 30), bearer: true));
        Assert.That(redrafted.GetProperty("status").GetString(), Is.EqualTo("Draft"));
        Assert.That(redrafted.GetProperty("totalRegularHours").GetDecimal(), Is.EqualTo(8m));
        await ReadAsync(await clinicianSession.SendAsync(HttpMethod.Post, $"/api/timesheets/{correctedId}/submit", bearer: true));

        var adjustment = await ReadAsync(await clinicianSession.SendAsync(HttpMethod.Post, "/api/timesheet-adjustments", new { timesheetEntryId = withHours.GetProperty("entries")[0].GetProperty("id").GetGuid(), adjustedHours = 9, reason = "Missed punch" }, bearer: true), HttpStatusCode.Created);
        Assert.That((await clinicianSession.SendAsync(HttpMethod.Patch, $"/api/timesheet-adjustments/{adjustment.GetProperty("id").GetGuid()}", new { status = "Approved" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var applied = await ReadAsync(await managerSession.SendAsync(HttpMethod.Patch, $"/api/timesheet-adjustments/{adjustment.GetProperty("id").GetGuid()}", new { status = "Approved" }, bearer: true));
        Assert.That(applied.GetProperty("status").GetString(), Is.EqualTo("Approved"));
        var adjustedSheet = await ReadAsync(await clinicianSession.SendAsync(HttpMethod.Get, $"/api/timesheets/{timesheetId}", bearer: true), HttpStatusCode.OK);
        Assert.That(adjustedSheet.GetProperty("totalRegularHours").GetDecimal() + adjustedSheet.GetProperty("totalOvertimeHours").GetDecimal(), Is.EqualTo(9m));
        var recruiterView = await ReadAsync(await recruiterSession.SendAsync(HttpMethod.Get, $"/api/timesheets/{timesheetId}", bearer: true), HttpStatusCode.OK);
        Assert.That(recruiterView.GetProperty("status").GetString(), Is.EqualTo("Approved"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.That(await db.AttendanceRecords.CountAsync(item => item.ShiftAssignmentId == firstAssignment && item.AttendanceStatus == AttendanceStatus.Completed), Is.EqualTo(1));
        Assert.That(await db.TimesheetApprovalHistory.CountAsync(item => item.TimesheetId == timesheetId && item.NewStatus == TimesheetStatus.Approved), Is.EqualTo(2));
        Assert.That((await adminSession.SendAsync(HttpMethod.Post, "/api/overtime-policies", new { jurisdictionCode = "TX", weeklyOvertimeThreshold = 40, workweekStartsOn = "Sunday" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    private async Task<Guid> PublishShiftAsync(Session manager, Guid facilityId, Guid locationId, Guid requisitionId, string start, string end)
    {
        var shiftId = (await ReadAsync(await manager.SendAsync(HttpMethod.Post, "/api/shifts", new
        {
            facilityId, facilityLocationId = locationId, jobRequisitionId = requisitionId, startLocal = start, endLocal = end, requiredClinicians = 1, specialty = "ICU"
        }, bearer: true))).GetProperty("id").GetGuid();
        Assert.That((await manager.SendAsync(HttpMethod.Patch, $"/api/shifts/{shiftId}/publish", bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return shiftId;
    }

    private static async Task<Guid> AssignAsync(Session manager, Session clinician, Guid shiftId, Guid profileId)
    {
        var assignmentId = (await ReadAsync(await manager.SendAsync(HttpMethod.Post, $"/api/shifts/{shiftId}/assignments", new { clinicianProfileId = profileId }, bearer: true), HttpStatusCode.Created)).GetProperty("id").GetGuid();
        Assert.That((await clinician.SendAsync(HttpMethod.Patch, $"/api/shifts/assignments/{assignmentId}/confirm", bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return assignmentId;
    }

    private static object Entry(Guid assignmentId, string start, string end, int breakMinutes) => new
    {
        shiftAssignmentId = assignmentId, startTime = start, endTime = end, breakMinutes, notes = "Worked shift"
    };

    private async Task<Guid> PublishRequisitionAsync(Session manager, Session admin, Guid facilityId, Guid locationId, string recruiterId)
    {
        var requisitionId = (await ReadAsync(await manager.SendAsync(HttpMethod.Post, "/api/requisitions", new
        {
            facilityId, locationId, jobTitle = "Timesheet Nurse", description = "Coverage used to verify attendance and timesheets.",
            specialty = "ICU", employmentType = "FullTime", shiftType = "Day", requiredExperience = 1, requiredCertifications = "RN",
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
            firstName = clinician.FirstName, lastName = "Timekeeping", phoneNumber = "555-0177", professionalTitle = "Registered Nurse",
            specialty = "ICU", yearsOfExperience = 4, preferredLocations = "Austin", employmentPreference = "FullTime", availabilityDate = "2026-12-01"
        }, bearer: true));
        var application = await ReadAsync(await session.SendAsync(HttpMethod.Post, "/api/applications", new { jobRequisitionId = requisitionId }, bearer: true), HttpStatusCode.Created);
        var applicationId = application.GetProperty("id").GetGuid();
        foreach (var status in new[] { "UnderReview", "Screening", "SubmittedToFacility" })
        {
            Assert.That((await (await LoginAsync(await RecruiterEmailAsync(requisitionId))).SendAsync(HttpMethod.Patch, $"/api/applications/{applicationId}/status", new { status, reason = status }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
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
        var email = $"phase7-{prefix}-{Guid.NewGuid():N}@smartstaff.test";
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FirstName = prefix, LastName = "Timekeeping", IsActive = true };
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

    private static object FacilityBody() => new
    {
        name = "Timesheet Medical", facilityType = "Hospital", description = "Attendance campus", address = "9 Clock Road", city = "Austin", state = "TX",
        zipCode = "78704", contactEmail = "ops@timesheet.test", contactPhone = "555-0170", status = "Active", timeZoneId = "America/Chicago"
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
