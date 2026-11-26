using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartStaff.Application.Authentication;
using SmartStaff.Application.Common;
using SmartStaff.Infrastructure.Persistence;

namespace SmartStaff.Infrastructure.Identity;

public sealed class AuthService : IAuthService, IProfileService, IUserAdminService
{
    private static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan ResetLifetime = TimeSpan.FromMinutes(30);

    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IAuditWriter _audit;
    private readonly IPasswordResetNotifier _resetNotifier;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> users,
        IAuditWriter audit,
        IPasswordResetNotifier resetNotifier,
        ILogger<AuthService> logger)
    {
        _dbContext = dbContext;
        _users = users;
        _audit = audit;
        _resetNotifier = resetNotifier;
        _logger = logger;
    }

    public async Task<OperationResult<AuthenticatedUserDto>> RegisterAsync(
        RegisterRequest request,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Role)
            && !string.Equals(request.Role, AppRoles.Clinician, StringComparison.Ordinal))
        {
            await _audit.WriteAsync("RegisterRejected", null, false, "Privileged role requested", ipAddress, cancellationToken);
            return OperationResult<AuthenticatedUserDto>.Fail(
                403,
                "Registration rejected",
                "Public registration can only create clinician accounts.");
        }

        var user = new ApplicationUser
        {
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim()
        };

        var created = await _users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            if (created.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName"))
            {
                await _audit.WriteAsync("RegisterRejected", null, false, "Duplicate email", ipAddress, cancellationToken);
                return OperationResult<AuthenticatedUserDto>.Fail(
                    409,
                    "Registration failed",
                    "An account with this email already exists.");
            }

            _logger.LogWarning("User registration failed because the password or account details were rejected.");
            return OperationResult<AuthenticatedUserDto>.Fail(
                400,
                "Registration failed",
                string.Join(" ", created.Errors.Select(error => error.Description)));
        }

        var roleResult = await _users.AddToRoleAsync(user, AppRoles.Clinician);
        if (!roleResult.Succeeded)
        {
            _logger.LogError("Clinician role assignment failed for user {UserId}", user.Id);
            return OperationResult<AuthenticatedUserDto>.Fail(
                500,
                "Registration failed",
                "The account could not be assigned the clinician role.");
        }

        await _audit.WriteAsync("Register", user.Id, true, "Clinician account created", ipAddress, cancellationToken);
        return OperationResult<AuthenticatedUserDto>.Ok(await ToDtoAsync(user));
    }

    public async Task<OperationResult<AuthSession>> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var user = await _users.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            await _audit.WriteAsync("LoginFailed", null, false, "Unknown account", ipAddress, cancellationToken);
            return InvalidCredentials();
        }

        if (await _users.IsLockedOutAsync(user))
        {
            await _audit.WriteAsync("LoginFailed", user.Id, false, "Account locked", ipAddress, cancellationToken);
            return Locked();
        }

        if (!user.IsActive)
        {
            await _audit.WriteAsync("LoginFailed", user.Id, false, "Account inactive", ipAddress, cancellationToken);
            return Inactive();
        }

        if (!await _users.CheckPasswordAsync(user, request.Password))
        {
            await _users.AccessFailedAsync(user);
            if (await _users.IsLockedOutAsync(user))
            {
                await _audit.WriteAsync("AccountLocked", user.Id, false, "Lockout threshold reached", ipAddress, cancellationToken);
                return Locked();
            }

            await _audit.WriteAsync("LoginFailed", user.Id, false, "Invalid credentials", ipAddress, cancellationToken);
            return InvalidCredentials();
        }

        await _users.ResetAccessFailedCountAsync(user);
        var session = await IssueRefreshTokenAsync(user, familyId: null, cancellationToken);
        await _audit.WriteAsync("LoginSucceeded", user.Id, true, null, ipAddress, cancellationToken);
        return OperationResult<AuthSession>.Ok(session);
    }

    public async Task<OperationResult<AuthSession>> RefreshAsync(
        string refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var hash = TokenHasher.Hash(refreshToken);
        var existing = await _dbContext.RefreshTokens.SingleOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);
        if (existing is null)
        {
            return OperationResult<AuthSession>.Fail(401, "Session expired", "Sign in again.");
        }

        if (existing.RevokedAt is not null || existing.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            if (existing.RevokedAt is not null)
            {
                await RevokeFamilyAsync(existing.FamilyId, cancellationToken);
                await _audit.WriteAsync("RefreshReuse", existing.UserId, false, "Revoked refresh token was presented again", ipAddress, cancellationToken);
            }

            return OperationResult<AuthSession>.Fail(401, "Session expired", "Sign in again.");
        }

        var user = await _users.FindByIdAsync(existing.UserId);
        if (user is null || await _users.IsLockedOutAsync(user) || !user.IsActive)
        {
            await RevokeFamilyAsync(existing.FamilyId, cancellationToken);
            return OperationResult<AuthSession>.Fail(401, "Session expired", "Sign in again.");
        }

        var replacement = TokenHasher.CreateSecret();
        existing.RevokedAt = DateTimeOffset.UtcNow;
        existing.ReplacedByHash = TokenHasher.Hash(replacement);
        var session = await IssueRefreshTokenAsync(user, existing.FamilyId, cancellationToken, replacement);
        await _audit.WriteAsync("Refresh", user.Id, true, null, ipAddress, cancellationToken);
        return OperationResult<AuthSession>.Ok(session);
    }

    public async Task<OperationResult<bool>> LogoutAsync(
        string? refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            var hash = TokenHasher.Hash(refreshToken);
            var existing = await _dbContext.RefreshTokens.SingleOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);
            if (existing is not null && existing.RevokedAt is null)
            {
                existing.RevokedAt = DateTimeOffset.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
                await _audit.WriteAsync("Logout", existing.UserId, true, null, ipAddress, cancellationToken);
                return OperationResult<bool>.Ok(true);
            }
        }

        await _audit.WriteAsync("Logout", null, true, "No active refresh token", ipAddress, cancellationToken);
        return OperationResult<bool>.Ok(true);
    }

    public async Task<OperationResult<bool>> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var user = await _users.FindByEmailAsync(request.Email.Trim());
        if (user is not null)
        {
            var outstanding = await _dbContext.PasswordResetTokens
                .Where(token => token.UserId == user.Id && token.UsedAt == null)
                .ToListAsync(cancellationToken);
            var now = DateTimeOffset.UtcNow;
            foreach (var token in outstanding)
            {
                token.UsedAt = now;
            }

            var resetToken = await _users.GeneratePasswordResetTokenAsync(user);
            var expiresAt = now.Add(ResetLifetime);
            _dbContext.PasswordResetTokens.Add(new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = TokenHasher.Hash(resetToken),
                CreatedAt = now,
                ExpiresAt = expiresAt
            });
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _resetNotifier.NotifyAsync(user.Email ?? request.Email, resetToken, expiresAt, cancellationToken);
            await _audit.WriteAsync("PasswordResetRequested", user.Id, true, null, ipAddress, cancellationToken);
        }
        else
        {
            await _audit.WriteAsync("PasswordResetRequested", null, true, "No matching account", ipAddress, cancellationToken);
        }

        return OperationResult<bool>.Ok(true);
    }

    public async Task<OperationResult<bool>> ResetPasswordAsync(
        ResetPasswordRequest request,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var generic = OperationResult<bool>.Fail(
            400,
            "Reset failed",
            "This password reset link is invalid or has expired.");

        var user = await _users.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            return generic;
        }

        var hash = TokenHasher.Hash(request.Token);
        var stored = await _dbContext.PasswordResetTokens
            .SingleOrDefaultAsync(token => token.TokenHash == hash && token.UserId == user.Id, cancellationToken);
        if (stored is null || stored.UsedAt is not null || stored.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            await _audit.WriteAsync("PasswordResetFailed", user.Id, false, "Expired or used token", ipAddress, cancellationToken);
            return generic;
        }

        var reset = await _users.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!reset.Succeeded)
        {
            return OperationResult<bool>.Fail(
                400,
                "Reset failed",
                string.Join(" ", reset.Errors.Select(error => error.Description)));
        }

        stored.UsedAt = DateTimeOffset.UtcNow;
        await RevokeFamilyForUserAsync(user.Id, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("PasswordResetCompleted", user.Id, true, null, ipAddress, cancellationToken);
        return OperationResult<bool>.Ok(true);
    }

    public async Task<OperationResult<AuthenticatedUserDto>> GetUserAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user is null)
        {
            return OperationResult<AuthenticatedUserDto>.Fail(401, "Session expired", "Sign in again.");
        }

        return OperationResult<AuthenticatedUserDto>.Ok(await ToDtoAsync(user));
    }

    public async Task<OperationResult<AuthenticatedUserDto>> UpdateAsync(
        string userId,
        UpdateProfileRequest request,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user is null)
        {
            return OperationResult<AuthenticatedUserDto>.Fail(401, "Session expired", "Sign in again.");
        }

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        var updated = await _users.UpdateAsync(user);
        if (!updated.Succeeded)
        {
            return OperationResult<AuthenticatedUserDto>.Fail(
                400,
                "Profile update failed",
                string.Join(" ", updated.Errors.Select(error => error.Description)));
        }

        await _audit.WriteAsync("ProfileUpdated", user.Id, true, null, ipAddress, cancellationToken);
        return OperationResult<AuthenticatedUserDto>.Ok(await ToDtoAsync(user));
    }

    public async Task<OperationResult<bool>> ChangePasswordAsync(
        string userId,
        ChangePasswordRequest request,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user is null || !user.IsActive)
        {
            return OperationResult<bool>.Fail(401, "Session expired", "Sign in again.");
        }

        var changed = await _users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!changed.Succeeded)
        {
            await _audit.WriteAsync("PasswordChangeFailed", user.Id, false, "Current password or policy rejected the change", ipAddress, cancellationToken);
            return OperationResult<bool>.Fail(
                400,
                "Password change failed",
                string.Join(" ", changed.Errors.Select(error => error.Description)));
        }

        await RevokeFamilyForUserAsync(user.Id, cancellationToken);
        await _audit.WriteAsync("PasswordChanged", user.Id, true, null, ipAddress, cancellationToken);
        return OperationResult<bool>.Ok(true);
    }

    public async Task<OperationResult<UserPageDto>> ListUsersAsync(UserListQuery query, CancellationToken cancellationToken)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 50 ? 10 : query.PageSize;
        var users = _users.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            users = users.Where(user =>
                (user.Email != null && user.Email.ToLower().Contains(search))
                || user.FirstName.ToLower().Contains(search)
                || user.LastName.ToLower().Contains(search));
        }

        if (query.Active is not null)
        {
            users = users.Where(user => user.IsActive == query.Active);
        }

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            var names = AppRoles.StoredNamesForFilter(query.Role).ToArray();
            var matchingIds =
                from userRole in _dbContext.UserRoles
                join role in _dbContext.Roles on userRole.RoleId equals role.Id
                where role.Name != null && names.Contains(role.Name)
                select userRole.UserId;
            users = users.Where(user => matchingIds.Contains(user.Id));
        }

        var total = await users.CountAsync(cancellationToken);
        var pageItems = await users
            .OrderBy(user => user.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var summaries = new List<UserSummaryDto>(pageItems.Count);
        foreach (var user in pageItems)
        {
            summaries.Add(await ToSummaryAsync(user));
        }

        return OperationResult<UserPageDto>.Ok(new UserPageDto(summaries, page, pageSize, total));
    }

    public async Task<OperationResult<UserSummaryDto>> UpdateRolesAsync(
        string actorUserId,
        string targetUserId,
        UpdateRolesRequest request,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var roles = request.Roles.Distinct(StringComparer.Ordinal).ToArray();
        if (roles.Length == 0 || roles.Any(role => !AppRoles.IsAssignable(role)))
        {
            return OperationResult<UserSummaryDto>.Fail(
                400,
                "Roles rejected",
                "Choose one or more recognized roles.");
        }

        var user = await _users.FindByIdAsync(targetUserId);
        if (user is null)
        {
            return OperationResult<UserSummaryDto>.Fail(404, "User not found", "That account does not exist.");
        }

        var current = await _users.GetRolesAsync(user);
        if (current.Contains(AppRoles.Administrator, StringComparer.Ordinal)
            && !roles.Contains(AppRoles.Administrator, StringComparer.Ordinal)
            && await AdministratorCountAsync() <= 1)
        {
            await _audit.WriteAsync("RolesRejected", actorUserId, false, "Last administrator role retained", ipAddress, cancellationToken);
            return OperationResult<UserSummaryDto>.Fail(
                409,
                "Roles rejected",
                "The last administrator role cannot be removed.");
        }
        var removal = await _users.RemoveFromRolesAsync(user, current);
        if (!removal.Succeeded)
        {
            return OperationResult<UserSummaryDto>.Fail(400, "Roles rejected", "The current roles could not be changed.");
        }

        var addition = await _users.AddToRolesAsync(user, roles);
        if (!addition.Succeeded)
        {
            return OperationResult<UserSummaryDto>.Fail(400, "Roles rejected", "The new roles could not be saved.");
        }

        await _audit.WriteAsync(
            "RolesChanged",
            actorUserId,
            true,
            $"Target {user.Id}",
            ipAddress,
            cancellationToken);
        return OperationResult<UserSummaryDto>.Ok(await ToSummaryAsync(user));
    }

    public async Task<OperationResult<UserSummaryDto>> SetActiveAsync(
        string actorUserId,
        string targetUserId,
        bool active,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var user = await _users.FindByIdAsync(targetUserId);
        if (user is null)
        {
            return OperationResult<UserSummaryDto>.Fail(404, "User not found", "That account does not exist.");
        }

        if (!active && await IsOnlyActiveAdministratorAsync(user))
        {
            await _audit.WriteAsync("AccountStatusRejected", actorUserId, false, "Last active administrator retained", ipAddress, cancellationToken);
            return OperationResult<UserSummaryDto>.Fail(
                409,
                "Account status rejected",
                "The last active administrator cannot be deactivated.");
        }

        user.IsActive = active;
        var updated = await _users.UpdateAsync(user);
        if (!updated.Succeeded)
        {
            return OperationResult<UserSummaryDto>.Fail(400, "Account status rejected", "The account status could not be saved.");
        }

        if (!active)
        {
            await RevokeFamilyForUserAsync(user.Id, cancellationToken);
        }

        await _audit.WriteAsync(active ? "AccountActivated" : "AccountDeactivated", actorUserId, true, $"Target {user.Id}", ipAddress, cancellationToken);
        return OperationResult<UserSummaryDto>.Ok(await ToSummaryAsync(user));
    }

    public async Task<OperationResult<IReadOnlyList<AuditLogDto>>> ListAuditLogsAsync(CancellationToken cancellationToken)
    {
        var logs = await _dbContext.AuditLogs
            .OrderByDescending(log => log.CreatedAt)
            .Take(100)
            .Select(log => new AuditLogDto(log.Id, log.Action, log.UserId, log.Succeeded, log.Detail, log.IpAddress, log.CreatedAt))
            .ToListAsync(cancellationToken);
        return OperationResult<IReadOnlyList<AuditLogDto>>.Ok(logs);
    }

    private async Task<AuthSession> IssueRefreshTokenAsync(
        ApplicationUser user,
        string? familyId,
        CancellationToken cancellationToken,
        string? secret = null)
    {
        var now = DateTimeOffset.UtcNow;
        secret ??= TokenHasher.CreateSecret();
        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = TokenHasher.Hash(secret),
            FamilyId = familyId ?? Guid.NewGuid().ToString("N"),
            CreatedAt = now,
            ExpiresAt = now.Add(RefreshLifetime)
        };
        _dbContext.RefreshTokens.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new AuthSession(await ToDtoAsync(user), secret, entity.ExpiresAt);
    }

    private async Task RevokeFamilyAsync(string familyId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var tokens = await _dbContext.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in tokens)
        {
            token.RevokedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task RevokeFamilyForUserAsync(string userId, CancellationToken cancellationToken)
    {
        var families = await _dbContext.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .Select(token => token.FamilyId)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var family in families)
        {
            await RevokeFamilyAsync(family, cancellationToken);
        }
    }

    private async Task<AuthenticatedUserDto> ToDtoAsync(ApplicationUser user)
    {
        var roles = await _users.GetRolesAsync(user);
        return new AuthenticatedUserDto(
            user.Id,
            user.Email ?? "",
            user.FirstName,
            user.LastName,
            roles.OrderBy(role => role, StringComparer.Ordinal).ToArray(),
            user.PhoneNumber);
    }

    private async Task<UserSummaryDto> ToSummaryAsync(ApplicationUser user)
    {
        var roles = await _users.GetRolesAsync(user);
        return new UserSummaryDto(
            user.Id,
            user.Email ?? "",
            user.FirstName,
            user.LastName,
            await _users.IsLockedOutAsync(user),
            user.IsActive,
            roles.OrderBy(role => role, StringComparer.Ordinal).ToArray());
    }

    private static OperationResult<AuthSession> InvalidCredentials() =>
        OperationResult<AuthSession>.Fail(
            401,
            "Sign-in failed",
            "Invalid email or password.");

    private static OperationResult<AuthSession> Locked() =>
        OperationResult<AuthSession>.Fail(
            423,
            "Account locked",
            "This account is temporarily locked. Try again later.");

    private static OperationResult<AuthSession> Inactive() =>
        OperationResult<AuthSession>.Fail(
            403,
            "Account inactive",
            "This account is inactive.");

    private async Task<int> AdministratorCountAsync()
    {
        var administrators = await _users.GetUsersInRoleAsync(AppRoles.Administrator);
        return administrators.Count;
    }

    private async Task<bool> IsOnlyActiveAdministratorAsync(ApplicationUser user)
    {
        if (!user.IsActive || !await _users.IsInRoleAsync(user, AppRoles.Administrator))
        {
            return false;
        }

        var administrators = await _users.GetUsersInRoleAsync(AppRoles.Administrator);
        return administrators.Count(administrator => administrator.IsActive) <= 1;
    }
}
