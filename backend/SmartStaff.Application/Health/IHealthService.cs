namespace SmartStaff.Application.Health;

public interface IHealthService
{
    Task<HealthReport> CheckAsync(CancellationToken cancellationToken);
}
