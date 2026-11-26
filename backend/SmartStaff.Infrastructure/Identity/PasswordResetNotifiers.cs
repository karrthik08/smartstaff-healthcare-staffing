using Microsoft.Extensions.Logging;
using SmartStaff.Application.Authentication;

namespace SmartStaff.Infrastructure.Identity;

public sealed class NullPasswordResetNotifier : IPasswordResetNotifier
{
    public Task NotifyAsync(string email, string resetToken, DateTimeOffset expiresAt, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

public sealed class DevelopmentPasswordResetNotifier : IPasswordResetNotifier
{
    private readonly ILogger<DevelopmentPasswordResetNotifier> _logger;
    private readonly string _path;

    public DevelopmentPasswordResetNotifier(ILogger<DevelopmentPasswordResetNotifier> logger)
    {
        _logger = logger;
        _path = Path.Combine(Directory.GetCurrentDirectory(), ".local", "password-resets.log");
    }

    public async Task NotifyAsync(string email, string resetToken, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var link = "http://127.0.0.1:5173/reset-password?email="
            + Uri.EscapeDataString(email)
            + "&token="
            + Uri.EscapeDataString(resetToken);
        await File.AppendAllTextAsync(
            _path,
            $"{DateTimeOffset.UtcNow:O} expires={expiresAt:O} {link}{Environment.NewLine}",
            cancellationToken);
        _logger.LogInformation("Password reset instructions were written to the local development sink.");
    }
}
