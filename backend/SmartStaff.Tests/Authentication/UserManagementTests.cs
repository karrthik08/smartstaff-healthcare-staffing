using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartStaff.API.Authentication;
using SmartStaff.Application.Authentication;
using SmartStaff.Infrastructure.Persistence;

namespace SmartStaff.Tests.Authentication;

[NonParallelizable]
public sealed class UserManagementTests
{
    private const string Password = "Phase3-Test-Passw0rd!";
    private const string ReplacementPassword = "Phase3-Changed-Passw0rd!";
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
    public void Tests_UseTheIsolatedDatabase()
    {
        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.GetDbConnection().Database;
        Assert.That(database, Is.EqualTo(TestDatabase.Name));
        Assert.That(Environment.GetEnvironmentVariable("POSTGRES_DB"), Is.EqualTo(TestDatabase.Name));
    }

    [Test]
    public async Task Bootstrap_CreatesAnAdministratorOnlyWhenNoneExists()
    {
        var email = NewEmail("bootstrap");
        var before = await AdministratorCountAsync();
        Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_EMAIL", email);
        Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_PASSWORD", Password);
        await using var factory = new AuthApiFactory();
        using var client = factory.CreateClient();
        await client.GetAsync("/api/health");
        Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_EMAIL", null);
        Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_PASSWORD", null);

        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var created = await users.FindByEmailAsync(email);
        if (before == 0)
        {
            Assert.That(created, Is.Not.Null);
            Assert.That(await users.IsInRoleAsync(created!, AppRoles.Administrator), Is.True);
        }
        else
        {
            Assert.That(created, Is.Null);
            Assert.That(await AdministratorCountAsync(), Is.EqualTo(before));
        }
    }

    [Test]
    public async Task Registration_RejectsRecruiterAndFacilityManagerRoles()
    {
        foreach (var role in new[] { AppRoles.Recruiter, AppRoles.FacilityManager })
        {
            var response = await _client.PostAsJsonAsync("/api/auth/register", new
            {
                email = NewEmail(role),
                password = Password,
                firstName = "No",
                lastName = "Privilege",
                role
            });
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }
    }

    [Test]
    public async Task Administrator_CanSearchAndDeactivate_AndClinicianCannotAdminister()
    {
        var clinicianEmail = NewEmail("clinician");
        var admin = await EnsureAdministratorAsync();
        var registered = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = clinicianEmail,
            password = Password,
            firstName = "Casey",
            lastName = "Searchable",
            phoneNumber = "555-0199"
        });
        Assert.That(registered.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var adminSession = await LoginAsync(admin.Email!, Password);
        var clinicianSession = await LoginAsync(clinicianEmail, Password);
        var search = await adminSession.SendAsync(HttpMethod.Get, $"/api/admin/users?search={Uri.EscapeDataString("Searchable")}&page=1&pageSize=5", bearer: true);
        Assert.That(search.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var page = await search.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(page.GetProperty("total").GetInt32(), Is.GreaterThanOrEqualTo(1));
        var match = page.GetProperty("items").EnumerateArray().First(item => item.GetProperty("email").GetString() == clinicianEmail);
        var clinicianId = match.GetProperty("id").GetString();

        var denied = await clinicianSession.SendAsync(
            HttpMethod.Put,
            $"/api/admin/users/{clinicianId}/status",
            new { active = false },
            bearer: true);
        Assert.That(denied.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        var roleDenied = await clinicianSession.SendAsync(
            HttpMethod.Put,
            $"/api/admin/users/{admin.Id}/roles",
            new { roles = new[] { AppRoles.Administrator, AppRoles.Recruiter } },
            bearer: true);
        Assert.That(roleDenied.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        var deactivated = await adminSession.SendAsync(
            HttpMethod.Put,
            $"/api/admin/users/{clinicianId}/status",
            new { active = false },
            bearer: true);
        Assert.That(deactivated.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var inactiveLogin = await _client.PostAsJsonAsync("/api/auth/login", new { email = clinicianEmail, password = Password });
        Assert.That(inactiveLogin.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        var logs = await adminSession.SendAsync(HttpMethod.Get, "/api/admin/audit-logs", bearer: true);
        var audit = await logs.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(audit.EnumerateArray().Any(entry => entry.GetProperty("action").GetString() == "AccountDeactivated"), Is.True);
    }

    [Test]
    public async Task Roles_CanOpenOnlyAuthorizedDashboards()
    {
        var admin = await EnsureAdministratorAsync();
        var recruiter = await CreateUserAsync("recruiter", AppRoles.Recruiter);
        var facility = await CreateUserAsync("facility", AppRoles.FacilityManager);
        var clinician = await CreateUserAsync("dashboard", AppRoles.Clinician);

        await ExpectDashboards(admin.Email!, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK);
        await ExpectDashboards(recruiter.Email!, HttpStatusCode.Forbidden, HttpStatusCode.OK, HttpStatusCode.Forbidden, HttpStatusCode.Forbidden);
        await ExpectDashboards(facility.Email!, HttpStatusCode.Forbidden, HttpStatusCode.Forbidden, HttpStatusCode.Forbidden, HttpStatusCode.OK);
        await ExpectDashboards(clinician.Email!, HttpStatusCode.Forbidden, HttpStatusCode.Forbidden, HttpStatusCode.OK, HttpStatusCode.Forbidden);

        var anonymous = await _client.GetAsync("/api/dashboards/clinician");
        Assert.That(anonymous.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    private async Task ExpectDashboards(
        string email,
        HttpStatusCode administrator,
        HttpStatusCode recruiter,
        HttpStatusCode clinician,
        HttpStatusCode facility)
    {
        var session = await LoginAsync(email, Password);
        var expected = new Dictionary<string, HttpStatusCode>
        {
            ["administrator"] = administrator,
            ["recruiter"] = recruiter,
            ["clinician"] = clinician,
            ["facility"] = facility
        };
        foreach (var entry in expected)
        {
            var response = await session.SendAsync(HttpMethod.Get, $"/api/dashboards/{entry.Key}", bearer: true);
            Assert.That(response.StatusCode, Is.EqualTo(entry.Value), $"{email} {entry.Key}");
        }
    }

    private async Task<ApplicationUser> CreateUserAsync(string prefix, string role)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = NewEmail(prefix);
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = prefix,
            LastName = "Role",
            IsActive = true
        };
        var created = await users.CreateAsync(user, Password);
        Assert.That(created.Succeeded, Is.True, string.Join(" ", created.Errors.Select(error => error.Description)));
        var assigned = await users.AddToRoleAsync(user, role);
        Assert.That(assigned.Succeeded, Is.True);
        return user;
    }

    [Test]
    public async Task Profile_UpdatesContactInformationAndPassword()
    {
        var email = NewEmail("profile");
        Assert.That((await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = Password,
            firstName = "Pat",
            lastName = "Profile"
        })).StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var session = await LoginAsync(email, Password);
        var updated = await session.SendAsync(HttpMethod.Put, "/api/profile", new
        {
            firstName = "Pat",
            lastName = "Profile",
            phoneNumber = "555-0142"
        }, bearer: true);
        Assert.That(updated.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await updated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetProperty("phoneNumber").GetString(), Is.EqualTo("555-0142"));

        var changed = await session.SendAsync(HttpMethod.Post, "/api/profile/password", new
        {
            currentPassword = Password,
            newPassword = ReplacementPassword
        }, bearer: true);
        Assert.That(changed.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password })).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await _client.PostAsJsonAsync("/api/auth/login", new { email, password = ReplacementPassword })).StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task LastActiveAdministrator_CannotBeDeactivated()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var keeper = await EnsureAdministratorAsync();
        var administrators = await users.GetUsersInRoleAsync(AppRoles.Administrator);
        foreach (var extra in administrators.Where(user => user.IsActive && user.Id != keeper.Id))
        {
            extra.IsActive = false;
            await users.UpdateAsync(extra);
        }

        var session = await LoginAsync(keeper.Email!, Password);
        var response = await session.SendAsync(HttpMethod.Put, $"/api/admin/users/{keeper.Id}/status", new { active = false }, bearer: true);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        var reloaded = await users.FindByIdAsync(keeper.Id);
        Assert.That(reloaded!.IsActive, Is.True);
    }

    private async Task<int> AdministratorCountAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return (await users.GetUsersInRoleAsync(AppRoles.Administrator)).Count;
    }

    private async Task<ApplicationUser> EnsureAdministratorAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var existing = (await users.GetUsersInRoleAsync(AppRoles.Administrator)).FirstOrDefault(user => user.IsActive);
        if (existing is not null && await users.CheckPasswordAsync(existing, Password))
        {
            return existing;
        }

        var email = NewEmail("admin");
        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = "Ada",
            LastName = "Admin",
            IsActive = true
        };
        var created = await users.CreateAsync(admin, Password);
        Assert.That(created.Succeeded, Is.True, string.Join(" ", created.Errors.Select(error => error.Description)));
        var role = await users.AddToRoleAsync(admin, AppRoles.Administrator);
        Assert.That(role.Succeeded, Is.True);
        return admin;
    }

    private async Task<Session> LoginAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new Session(_client, body.GetProperty("accessToken").GetString()!);
    }

    private static string NewEmail(string prefix) =>
        $"phase3-{prefix}-{Guid.NewGuid():N}@smartstaff.test";

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
