using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartStaff.API.Authentication;
using SmartStaff.Application.Authentication;
using SmartStaff.Infrastructure.Identity;
using SmartStaff.Infrastructure.Persistence;

namespace SmartStaff.Tests.Authentication;

[SetUpFixture]
public sealed class AuthTestEnvironment
{
    public const string JwtSigningKey = "smartstaff-test-signing-key-not-for-production";

    [OneTimeSetUp]
    public void Configure()
    {
        Environment.SetEnvironmentVariable("JWT_SIGNING_KEY", JwtSigningKey);
        Environment.SetEnvironmentVariable("AUTH_RATE_LIMIT_PER_MINUTE", "1000");
    }
}

public sealed class CapturingResetNotifier : IPasswordResetNotifier
{
    public List<(string Email, string Token)> Messages { get; } = [];

    public Task NotifyAsync(string email, string resetToken, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        Messages.Add((email, resetToken));
        return Task.CompletedTask;
    }
}

public class AuthApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPasswordResetNotifier>();
            services.AddSingleton<CapturingResetNotifier>();
            services.AddSingleton<IPasswordResetNotifier>(provider => provider.GetRequiredService<CapturingResetNotifier>());
        });
    }
}

[NonParallelizable]
public sealed class AuthenticationTests
{
    private const string Password = "Phase2-Test-Passw0rd!";
    private AuthApiFactory _factory = null!;
    private HttpClient _client = null!;
    private ApiSession _session = null!;

    [OneTimeSetUp]
    public void Start()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("POSTGRES_PASSWORD")))
        {
            Assert.Ignore("POSTGRES_PASSWORD is not set.");
        }

        _factory = new AuthApiFactory();
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        _session = new ApiSession(_client);
    }

    [OneTimeTearDown]
    public async Task Stop()
    {
        if (_factory is null)
        {
            return;
        }

        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Test]
    public void PasswordHasher_DoesNotStoreThePlaintextPassword()
    {
        var hasher = new PasswordHasher<ApplicationUser>();
        var user = new ApplicationUser { UserName = "hash@smartstaff.test" };
        var hash = hasher.HashPassword(user, Password);

        Assert.That(hash, Is.Not.EqualTo(Password));
        Assert.That(hash, Does.Not.Contain(Password));
        Assert.That(hasher.VerifyHashedPassword(user, hash, Password), Is.EqualTo(PasswordVerificationResult.Success));
    }

    [Test]
    public async Task Register_CreatesAClinicianAndRejectsADuplicateEmail()
    {
        var email = NewEmail();
        var created = await _session.SendAsync(HttpMethod.Post, "/api/auth/register", NewRegistration(email));
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.That(user, Is.Not.Null);
        Assert.That(user!.PasswordHash, Is.Not.EqualTo(Password));
        Assert.That(user.PasswordHash, Does.Not.Contain(Password));
        Assert.That(await userManager.IsInRoleAsync(user, AppRoles.Clinician), Is.True);
        Assert.That(await db.Users.CountAsync(candidate => candidate.Email == email), Is.EqualTo(1));

        var duplicate = await _session.SendAsync(HttpMethod.Post, "/api/auth/register", NewRegistration(email));
        Assert.That(duplicate.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task Register_RejectsAPrivilegedRole()
    {
        var email = NewEmail();
        var response = await _session.SendAsync(HttpMethod.Post, "/api/auth/register", NewRegistration(email, AppRoles.Administrator));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.That(await users.FindByEmailAsync(email), Is.Null);
    }

    [Test]
    public async Task Login_IssuesAJwtAndRejectsABadPassword()
    {
        var email = await RegisterClinician();
        var bad = await _session.SendAsync(HttpMethod.Post, "/api/auth/login", new { email, password = "wrong-password" });
        var badBody = await bad.Content.ReadAsStringAsync();
        Assert.That(bad.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(badBody, Does.Not.Contain("wrong-password"));

        var good = await _session.SendAsync(HttpMethod.Post, "/api/auth/login", new { email, password = Password });
        Assert.That(good.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var token = _session.ReadAccessToken(good);
        Assert.That(token, Is.Not.Empty);

        var me = await _session.SendAsync(HttpMethod.Get, "/api/auth/me", bearer: true);
        Assert.That(me.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var anonymous = await _session.SendAsync(HttpMethod.Get, "/api/auth/me");
        Assert.That(anonymous.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var tampered = await _session.SendAsync(HttpMethod.Get, "/api/auth/me", bearerToken: token + "x");
        Assert.That(tampered.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Refresh_RotatesTokensAndRejectsReuse()
    {
        var email = await RegisterClinician();
        var login = await _session.SendAsync(HttpMethod.Post, "/api/auth/login", new { email, password = Password });
        var originalRefresh = _session.Cookie(AuthCookies.RefreshName);
        _session.UseAccessToken(_session.ReadAccessToken(login));

        var refreshed = await _session.SendAsync(HttpMethod.Post, "/api/auth/refresh", csrf: true);
        Assert.That(refreshed.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var rotatedRefresh = _session.Cookie(AuthCookies.RefreshName);
        Assert.That(rotatedRefresh, Is.Not.EqualTo(originalRefresh));
        _session.UseAccessToken(_session.ReadAccessToken(refreshed));

        var me = await _session.SendAsync(HttpMethod.Get, "/api/auth/me", bearer: true);
        Assert.That(me.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var csrf = _session.Cookie(AuthCookies.CsrfName);
        _session.SetCookie(AuthCookies.RefreshName, originalRefresh);
        var reuse = await _session.SendAsync(HttpMethod.Post, "/api/auth/refresh", csrf: true);
        Assert.That(reuse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        _session.SetCookie(AuthCookies.RefreshName, rotatedRefresh);
        _session.SetCookie(AuthCookies.CsrfName, csrf);
        var revokedFamily = await _session.SendAsync(HttpMethod.Post, "/api/auth/refresh", csrf: true);
        Assert.That(revokedFamily.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Login_LocksTheAccountAfterRepeatedFailures()
    {
        var email = await RegisterClinician();
        HttpResponseMessage? last = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            last = await _session.SendAsync(HttpMethod.Post, "/api/auth/login", new { email, password = "Wrong-password-1!" });
        }

        Assert.That(last!.StatusCode, Is.EqualTo(HttpStatusCode.Locked));
        var correct = await _session.SendAsync(HttpMethod.Post, "/api/auth/login", new { email, password = Password });
        Assert.That(correct.StatusCode, Is.EqualTo(HttpStatusCode.Locked));
    }

    [Test]
    public async Task Authorization_DeniesClinicianAccessToAdministration()
    {
        var email = await RegisterClinician();
        await _session.SendAsync(HttpMethod.Post, "/api/auth/login", new { email, password = Password });
        var users = await _session.SendAsync(HttpMethod.Get, "/api/admin/users", bearer: true);
        Assert.That(users.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        var clinician = await _session.SendAsync(HttpMethod.Get, "/api/dashboards/clinician", bearer: true);
        Assert.That(clinician.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var admin = await _session.SendAsync(HttpMethod.Get, "/api/dashboards/administrator", bearer: true);
        Assert.That(admin.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        var roles = await _session.SendAsync(
            HttpMethod.Put,
            "/api/admin/users/someone/roles",
            new { roles = new[] { AppRoles.Administrator } },
            bearer: true);
        Assert.That(roles.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Logout_RejectsThePreviousSession()
    {
        var email = await RegisterClinician();
        await _session.SendAsync(HttpMethod.Post, "/api/auth/login", new { email, password = Password });
        var refreshCookie = _session.Cookie(AuthCookies.RefreshName);
        var csrf = _session.Cookie(AuthCookies.CsrfName);
        var logout = await _session.SendAsync(HttpMethod.Post, "/api/auth/logout", csrf: true);
        Assert.That(logout.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var revokedAccess = await _session.SendAsync(HttpMethod.Get, "/api/auth/me", bearer: true);
        Assert.That(revokedAccess.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        _session.SetCookie(AuthCookies.RefreshName, refreshCookie);
        _session.SetCookie(AuthCookies.CsrfName, csrf);
        var refresh = await _session.SendAsync(HttpMethod.Post, "/api/auth/refresh", csrf: true);
        Assert.That(refresh.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var me = await _session.SendAsync(HttpMethod.Get, "/api/auth/me");
        Assert.That(me.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task ResetPassword_RejectsAnExpiredTokenAndASecondUse()
    {
        var email = await RegisterClinician();
        var notifier = _factory.Services.GetRequiredService<CapturingResetNotifier>();
        notifier.Messages.Clear();

        var requested = await _session.SendAsync(HttpMethod.Post, "/api/auth/forgot-password", new { email });
        Assert.That(requested.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var token = notifier.Messages.Last(message => message.Email == email).Token;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stored = await db.PasswordResetTokens.SingleAsync(candidate => candidate.TokenHash == TokenHasher.Hash(token));
            stored.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        var expired = await _session.SendAsync(HttpMethod.Post, "/api/auth/reset-password", new
        {
            email,
            token,
            newPassword = "Replacement-Passw0rd!"
        });
        Assert.That(expired.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        notifier.Messages.Clear();
        await _session.SendAsync(HttpMethod.Post, "/api/auth/forgot-password", new { email });
        var fresh = notifier.Messages.Last(message => message.Email == email).Token;
        var reset = await _session.SendAsync(HttpMethod.Post, "/api/auth/reset-password", new
        {
            email,
            token = fresh,
            newPassword = "Replacement-Passw0rd!"
        });
        Assert.That(reset.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var reused = await _session.SendAsync(HttpMethod.Post, "/api/auth/reset-password", new
        {
            email,
            token = fresh,
            newPassword = "Another-Passw0rd!"
        });
        Assert.That(reused.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    private async Task<string> RegisterClinician()
    {
        var email = NewEmail();
        var response = await _session.SendAsync(HttpMethod.Post, "/api/auth/register", NewRegistration(email));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        return email;
    }

    private static string NewEmail() => $"phase2-{Guid.NewGuid():N}@smartstaff.test";

    private static object NewRegistration(string email, string? role = null) => new
    {
        email,
        password = Password,
        firstName = "Test",
        lastName = "Clinician",
        phoneNumber = "555-0100",
        role
    };

    private sealed class ApiSession
    {
        private readonly HttpClient _client;
        private readonly Dictionary<string, string> _cookies = new(StringComparer.Ordinal);
        private string? _accessToken;

        public ApiSession(HttpClient client)
        {
            _client = client;
        }

        public string Cookie(string name) => _cookies[name];

        public void SetCookie(string name, string value) => _cookies[name] = value;

        public void UseAccessToken(string token) => _accessToken = token;

        public async Task<HttpResponseMessage> SendAsync(
            HttpMethod method,
            string url,
            object? body = null,
            bool csrf = false,
            bool bearer = false,
            string? bearerToken = null)
        {
            var request = new HttpRequestMessage(method, url);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body);
            }

            if (_cookies.Count > 0)
            {
                request.Headers.TryAddWithoutValidation(
                    "Cookie",
                    string.Join("; ", _cookies.Select(cookie => $"{cookie.Key}={cookie.Value}")));
            }

            if (csrf && _cookies.TryGetValue(AuthCookies.CsrfName, out var csrfToken))
            {
                request.Headers.TryAddWithoutValidation(AuthCookies.CsrfHeader, csrfToken);
            }

            var token = bearerToken ?? (bearer ? _accessToken : null);
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }

            var response = await _client.SendAsync(request);
            if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
            {
                foreach (var cookie in cookies)
                {
                    Store(cookie);
                }
            }

            if (response.StatusCode == HttpStatusCode.OK)
            {
                var media = response.Content.Headers.ContentType?.MediaType;
                if (media == "application/json")
                {
                    var document = await response.Content.ReadFromJsonAsync<JsonElement>();
                    if (document.TryGetProperty("accessToken", out var accessToken))
                    {
                        _accessToken = accessToken.GetString();
                    }
                }
            }

            return response;
        }

        public string ReadAccessToken(HttpResponseMessage response)
        {
            Assert.That(_accessToken, Is.Not.Null);
            return _accessToken!;
        }

        private void Store(string setCookie)
        {
            var pair = setCookie.Split(';', 2)[0];
            var split = pair.Split('=', 2);
            if (split.Length == 2)
            {
                _cookies[split[0]] = split[1];
            }
        }
    }
}
