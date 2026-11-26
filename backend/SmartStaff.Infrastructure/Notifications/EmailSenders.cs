using Microsoft.Extensions.Logging;
using SmartStaff.Infrastructure.Persistence;

namespace SmartStaff.Infrastructure.Notifications;

public sealed class RecordingEmailSender : IEmailSender
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<RecordingEmailSender> _logger;

    public RecordingEmailSender(ApplicationDbContext db, ILogger<RecordingEmailSender> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _db.EmailDispatches.Add(new Domain.Notifications.EmailDispatch
        {
            Id = Guid.NewGuid(),
            RecipientUserId = message.RecipientUserId,
            Subject = message.Subject,
            Body = message.Body,
            CreatedAt = DateTimeOffset.UtcNow
        });
        _logger.LogInformation("Recorded email for {RecipientUserId} with subject {Subject}", message.RecipientUserId, message.Subject);
        return Task.CompletedTask;
    }
}

/// <summary>Prepared for a later deployment. It is not registered unless email delivery is explicitly switched on.</summary>
public sealed class SesEmailSender : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("AWS SES is not enabled for this environment.");
}
