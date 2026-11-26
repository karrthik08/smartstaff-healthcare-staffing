using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartStaff.Application.Notifications;

namespace SmartStaff.Infrastructure.Notifications;

public sealed class NotificationHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationHostedService> _logger;

    public NotificationHostedService(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<NotificationHostedService> logger)
    {
        _scopes = scopes;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<INotificationProcessor>().ProcessAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError("Notification background processing failed. Cause {ExceptionType}", exception.GetType().Name);
            }

            var seconds = int.TryParse(_configuration["NOTIFICATION_POLL_SECONDS"], out var configured) && configured > 0 ? configured : 2;
            await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken);
        }
    }
}
