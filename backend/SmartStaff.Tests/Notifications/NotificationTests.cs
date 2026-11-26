using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartStaff.Application.Authentication;
using SmartStaff.Application.Notifications;
using SmartStaff.Domain.Notifications;
using SmartStaff.Infrastructure.Notifications;
using SmartStaff.Infrastructure.Persistence;
using SmartStaff.Tests.Authentication;

namespace SmartStaff.Tests.Notifications;

[NonParallelizable]
public sealed class NotificationTests
{
    private const string Password = "Phase8-Test-Passw0rd!";
    private NotificationApiFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void Start()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("POSTGRES_PASSWORD")))
        {
            Assert.Ignore("POSTGRES_PASSWORD is not set.");
        }

        _factory = new NotificationApiFactory();
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
    public async Task Notifications_RespectPreferencesRetriesAndOwnership()
    {
        var admin = await CreateUserAsync("admin", AppRoles.Administrator);
        var manager = await CreateUserAsync("manager", AppRoles.FacilityManager);
        var clinician = await CreateUserAsync("clinician", AppRoles.Clinician);
        var outsider = await CreateUserAsync("outsider", AppRoles.Clinician);
        var adminSession = await LoginAsync(admin.Email!);
        var managerSession = await LoginAsync(manager.Email!);
        var facilityId = (await ReadAsync(await adminSession.SendAsync(HttpMethod.Post, "/api/facilities", new
        {
            name = "Notify Medical", facilityType = "Hospital", description = "Notification campus", address = "1 Bell Road", city = "Austin", state = "TX",
            zipCode = "78704", contactEmail = "ops@notify.test", contactPhone = "555-0188", status = "Active", timeZoneId = "America/Chicago"
        }, bearer: true))).GetProperty("id").GetGuid();
        var locationId = (await ReadAsync(await adminSession.SendAsync(HttpMethod.Post, $"/api/facilities/{facilityId}/locations", new
        {
            locationName = "Bell Wing", address = "1 Bell Road", city = "Austin", state = "TX", zipCode = "78704", isActive = true
        }, bearer: true))).GetProperty("id").GetGuid();
        Assert.That((await adminSession.SendAsync(HttpMethod.Post, $"/api/facilities/{facilityId}/members", new { userId = manager.Id, membershipRole = "Manager" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var requisitionId = (await ReadAsync(await managerSession.SendAsync(HttpMethod.Post, "/api/requisitions", new
        {
            facilityId, locationId, jobTitle = "Notify Nurse", description = "Coverage used to verify notifications.",
            specialty = "ICU", employmentType = "FullTime", shiftType = "Day", requiredExperience = 1, requiredCertifications = "RN",
            openPositions = 1, payRateMin = 40, payRateMax = 60, startDate = "2026-12-01"
        }, bearer: true))).GetProperty("id").GetGuid();
        Assert.That((await managerSession.SendAsync(HttpMethod.Patch, $"/api/requisitions/{requisitionId}/status", new { status = "Open" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await managerSession.SendAsync(HttpMethod.Patch, $"/api/requisitions/{requisitionId}/status", new { status = "Published" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var clinicianSession = await LoginAsync(clinician.Email!);
        var profileId = (await ReadAsync(await clinicianSession.SendAsync(HttpMethod.Put, "/api/clinicians/me", new
        {
            firstName = "Casey", lastName = "Notify", phoneNumber = "555-0188", professionalTitle = "Registered Nurse",
            specialty = "ICU", yearsOfExperience = 3, preferredLocations = "Austin", employmentPreference = "FullTime", availabilityDate = "2026-12-01"
        }, bearer: true))).GetProperty("id").GetGuid();
        var applicationId = (await ReadAsync(await clinicianSession.SendAsync(HttpMethod.Post, "/api/applications", new { jobRequisitionId = requisitionId }, bearer: true), HttpStatusCode.Created)).GetProperty("id").GetGuid();
        foreach (var status in new[] { "UnderReview", "Screening", "SubmittedToFacility" })
        {
            Assert.That((await adminSession.SendAsync(HttpMethod.Patch, $"/api/applications/{applicationId}/status", new { status, reason = status }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        Assert.That((await managerSession.SendAsync(HttpMethod.Patch, $"/api/applications/{applicationId}/status", new { status = "Interview", reason = "Interview" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await managerSession.SendAsync(HttpMethod.Patch, $"/api/applications/{applicationId}/status", new { status = "Offer", reason = "Offer" }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await managerSession.SendAsync(HttpMethod.Post, "/api/placements", new { jobApplicationId = applicationId, startDate = "2026-12-01", agreedPayRate = 52 }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var shiftId = (await ReadAsync(await managerSession.SendAsync(HttpMethod.Post, "/api/shifts", new
        {
            facilityId, facilityLocationId = locationId, jobRequisitionId = requisitionId, startLocal = "2026-12-22T07:00:00", endLocal = "2026-12-22T15:00:00", requiredClinicians = 1, specialty = "ICU"
        }, bearer: true))).GetProperty("id").GetGuid();
        Assert.That((await managerSession.SendAsync(HttpMethod.Patch, $"/api/shifts/{shiftId}/publish", bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        await ProcessAsync();

        var mine = await ReadAsync(await clinicianSession.SendAsync(HttpMethod.Get, "/api/notifications", bearer: true), HttpStatusCode.OK);
        Assert.That(mine.ToString(), Does.Contain("ShiftPublished"));
        var notificationId = mine.GetProperty("items").EnumerateArray().First(item => item.GetProperty("notificationType").GetString() == "ShiftPublished").GetProperty("id").GetGuid();
        var before = mine.GetProperty("total").GetInt32();
        await ProcessAsync();
        var again = await ReadAsync(await clinicianSession.SendAsync(HttpMethod.Get, "/api/notifications", bearer: true), HttpStatusCode.OK);
        Assert.That(again.GetProperty("total").GetInt32(), Is.EqualTo(before));
        Assert.That((await (await LoginAsync(outsider.Email!)).SendAsync(HttpMethod.Patch, $"/api/notifications/{notificationId}/read", bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var read = await ReadAsync(await clinicianSession.SendAsync(HttpMethod.Patch, $"/api/notifications/{notificationId}/read", bearer: true), HttpStatusCode.OK);
        Assert.That(read.GetProperty("isRead").GetBoolean(), Is.True);
        Assert.That((await clinicianSession.SendAsync(HttpMethod.Put, "/api/notifications/preferences", new
        {
            emailEnabled = true, inAppEnabled = true, shiftRemindersEnabled = false, applicationUpdatesEnabled = true, timesheetUpdatesEnabled = true
        }, bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var secondShift = (await ReadAsync(await managerSession.SendAsync(HttpMethod.Post, "/api/shifts", new
        {
            facilityId, facilityLocationId = locationId, jobRequisitionId = requisitionId, startLocal = "2026-12-23T07:00:00", endLocal = "2026-12-23T15:00:00", requiredClinicians = 1, specialty = "ICU"
        }, bearer: true))).GetProperty("id").GetGuid();
        Assert.That((await managerSession.SendAsync(HttpMethod.Patch, $"/api/shifts/{secondShift}/publish", bearer: true)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        await ProcessAsync();
        var afterPreference = await ReadAsync(await clinicianSession.SendAsync(HttpMethod.Get, "/api/notifications", bearer: true), HttpStatusCode.OK);
        Assert.That(afterPreference.GetProperty("items").EnumerateArray().Count(item => item.GetProperty("notificationType").GetString() == "ShiftPublished"), Is.EqualTo(1));

        FlakyEmailSender.Failures = 50;
        var retryTitle = $"Retry {Guid.NewGuid():N}";
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.NotificationOutbox.Where(item => item.ProcessedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.AvailableAt, DateTimeOffset.UtcNow.AddDays(7)));
            db.NotificationOutbox.Add(new NotificationOutbox
            {
                Id = Guid.NewGuid(),
                IdempotencyKey = $"Retry:{Guid.NewGuid():N}",
                RecipientUserId = clinician.Id!,
                NotificationType = "TimesheetApproved",
                Title = retryTitle,
                Message = "Delivery should retry.",
                RelatedEntityType = "Timesheet",
                RelatedEntityId = Guid.NewGuid(),
                AvailableAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        await ProcessAsync();
        await using var check = _factory.Services.CreateAsyncScope();
        var stored = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var retry = await stored.NotificationOutbox.SingleAsync(item => item.Title == retryTitle);
        Assert.That(retry.AttemptCount, Is.GreaterThanOrEqualTo(1));
        Assert.That(retry.ProcessedAt, Is.Null);
        retry.AvailableAt = DateTimeOffset.UtcNow;
        await stored.SaveChangesAsync();
        FlakyEmailSender.Failures = 0;
        await ProcessAsync();
        await stored.Entry(retry).ReloadAsync();
        Assert.That(retry.ProcessedAt, Is.Not.Null);
        Assert.That(profileId, Is.Not.EqualTo(Guid.Empty));
    }

    private async Task ProcessAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<INotificationProcessor>().ProcessAsync(CancellationToken.None);
    }

    private async Task<ApplicationUser> CreateUserAsync(string prefix, string role)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"phase8-{prefix}-{Guid.NewGuid():N}@smartstaff.test";
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FirstName = prefix, LastName = "Notify", IsActive = true };
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

    private sealed class NotificationApiFactory : AuthApiFactory
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseSetting("NOTIFICATION_POLL_SECONDS", "3600");
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddScoped<IEmailSender, FlakyEmailSender>();
            });
        }
    }

    private sealed class FlakyEmailSender : IEmailSender
    {
        public static int Failures;

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            if (Interlocked.Decrement(ref Failures) >= 0)
            {
                throw new InvalidOperationException("mailbox");
            }

            return Task.CompletedTask;
        }
    }
}
