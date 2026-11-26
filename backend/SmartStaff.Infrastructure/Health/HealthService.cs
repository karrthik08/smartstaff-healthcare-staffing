using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartStaff.Application.Health;
using SmartStaff.Infrastructure.Persistence;

namespace SmartStaff.Infrastructure.Health;

public sealed class HealthService : IHealthService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<HealthService> _logger;

    public HealthService(ApplicationDbContext dbContext, ILogger<HealthService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<HealthReport> CheckAsync(CancellationToken cancellationToken)
    {
        var checkedAtUtc = DateTimeOffset.UtcNow;

        try
        {
            var connected = await _dbContext.Database.CanConnectAsync(cancellationToken);
            if (!connected)
            {
                _logger.LogWarning("PostgreSQL health check could not open a connection.");
            }

            return HealthReport.FromDatabase(
                connected,
                connected ? null : "PostgreSQL did not accept a connection.",
                checkedAtUtc);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "PostgreSQL health check failed.");
            return HealthReport.FromDatabase(false, exception.Message, checkedAtUtc);
        }
    }
}
