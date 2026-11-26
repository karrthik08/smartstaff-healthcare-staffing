namespace SmartStaff.Infrastructure.Persistence;

public sealed class AuditLog
{
    public Guid Id { get; set; }

    public string Action { get; set; } = "";

    public string? UserId { get; set; }

    public bool Succeeded { get; set; }

    public string? Detail { get; set; }

    public string? IpAddress { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class PasswordResetToken
{
    public Guid Id { get; set; }

    public string UserId { get; set; } = "";

    public string TokenHash { get; set; } = "";

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UsedAt { get; set; }
}

public sealed class RefreshToken
{
    public Guid Id { get; set; }

    public string UserId { get; set; } = "";

    public string TokenHash { get; set; } = "";

    public string FamilyId { get; set; } = "";

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? ReplacedByHash { get; set; }
}
