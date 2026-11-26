using Microsoft.Extensions.Logging;
using SmartStaff.Infrastructure.Persistence;

namespace SmartStaff.Infrastructure.Identity;

public interface IAuditWriter
{
    Task WriteAsync(string action, string? userId, bool succeeded, string? detail, string? ipAddress, CancellationToken cancellationToken);
}

public sealed class AuditWriter : IAuditWriter
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<AuditWriter> _logger;

    public AuditWriter(ApplicationDbContext dbContext, ILogger<AuditWriter> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task WriteAsync(
        string action,
        string? userId,
        bool succeeded,
        string? detail,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var safeDetail = detail is { Length: > 500 } ? detail[..500] : detail;
        _dbContext.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            Action = action,
            UserId = userId,
            Succeeded = succeeded,
            Detail = safeDetail,
            IpAddress = ipAddress,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Authentication audit {Action} succeeded {Succeeded} for user {UserId}",
            action,
            succeeded,
            userId ?? "none");
    }
}
