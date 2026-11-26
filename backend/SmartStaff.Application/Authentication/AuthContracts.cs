namespace SmartStaff.Application.Authentication;

public sealed record AuthenticatedUserDto(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    IReadOnlyList<string> Roles);

public sealed record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string? Role);

public sealed record LoginRequest(string Email, string Password);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);

public sealed record UpdateProfileRequest(string FirstName, string LastName, string? PhoneNumber);

public sealed record UpdateRolesRequest(IReadOnlyList<string> Roles);

public sealed record AuthSession(AuthenticatedUserDto User, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);

public sealed record DashboardDto(string Workspace, string Title, string Message, DateTimeOffset GeneratedAtUtc);

public sealed record UserSummaryDto(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    bool Locked,
    IReadOnlyList<string> Roles);

public sealed record AuditLogDto(
    Guid Id,
    string Action,
    string? UserId,
    bool Succeeded,
    string? Detail,
    string? IpAddress,
    DateTimeOffset CreatedAt);
