using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartStaff.Application.Notifications;
using SmartStaff.Domain.Notifications;
using SmartStaff.Infrastructure.Persistence;

namespace SmartStaff.Infrastructure.Notifications;

public sealed class NotificationProcessor : INotificationProcessor
{
    private readonly ApplicationDbContext _db;
    private readonly IEmailSender _email;
    private readonly ILogger<NotificationProcessor> _logger;

    public NotificationProcessor(ApplicationDbContext db, IEmailSender email, ILogger<NotificationProcessor> logger)
    {
        _db = db;
        _email = email;
        _logger = logger;
    }

    public async Task<int> ProcessAsync(CancellationToken cancellationToken)
    {
        await QueueRemindersAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var batch = await _db.NotificationOutbox
            .Where(item => item.ProcessedAt == null && item.AvailableAt <= now)
            .OrderBy(item => item.CreatedAt)
            .Take(25)
            .ToListAsync(cancellationToken);
        var processed = 0;
        foreach (var message in batch)
        {
            try
            {
                await DeliverAsync(message, now, cancellationToken);
                message.ProcessedAt = DateTimeOffset.UtcNow;
                message.LastError = null;
                processed++;
                _logger.LogInformation("Processed notification outbox {IdempotencyKey} for {RecipientUserId}", message.IdempotencyKey, message.RecipientUserId);
            }
            catch (Exception exception)
            {
                message.AttemptCount++;
                message.LastError = exception.GetType().Name;
                if (message.AttemptCount >= 8)
                {
                    message.ProcessedAt = DateTimeOffset.UtcNow;
                    _logger.LogWarning(
                        "Notification delivery stopped for {IdempotencyKey} after {AttemptCount} attempts. Cause {ExceptionType}",
                        message.IdempotencyKey,
                        message.AttemptCount,
                        exception.GetType().Name);
                }
                else
                {
                    var delaySeconds = Math.Pow(2, Math.Min(message.AttemptCount, 8));
                    message.AvailableAt = DateTimeOffset.UtcNow.AddSeconds(delaySeconds);
                    _logger.LogWarning(
                        "Notification delivery retry scheduled for {IdempotencyKey} after attempt {AttemptCount}. Cause {ExceptionType}",
                        message.IdempotencyKey,
                        message.AttemptCount,
                        exception.GetType().Name);
                }
            }
        }

        if (batch.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return processed;
    }

    private async Task DeliverAsync(NotificationOutbox message, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await _db.Notifications.AnyAsync(item => item.IdempotencyKey == message.IdempotencyKey, cancellationToken))
        {
            return;
        }

        var preference = await _db.NotificationPreferences.AsNoTracking().FirstOrDefaultAsync(item => item.UserId == message.RecipientUserId, cancellationToken);
        if (!Allows(preference, message.NotificationType))
        {
            _logger.LogInformation("Suppressed notification {NotificationType} for {RecipientUserId}", message.NotificationType, message.RecipientUserId);
            return;
        }

        var sendEmail = preference?.EmailEnabled != false;
        if (sendEmail)
        {
            var template = await _db.NotificationTemplates.AsNoTracking().FirstOrDefaultAsync(item => item.TemplateKey == message.NotificationType && item.IsActive, cancellationToken);
            var subject = template?.Subject.Replace("{title}", message.Title, StringComparison.Ordinal) ?? message.Title;
            var body = template?.Body.Replace("{message}", message.Message, StringComparison.Ordinal) ?? message.Message;
            await _email.SendAsync(new EmailMessage(message.RecipientUserId, subject, body), cancellationToken);
        }

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            RecipientUserId = message.RecipientUserId,
            NotificationType = message.NotificationType,
            Title = message.Title,
            Message = message.Message,
            RelatedEntityType = message.RelatedEntityType,
            RelatedEntityId = message.RelatedEntityId,
            IdempotencyKey = message.IdempotencyKey,
            CreatedAt = now
        };
        _db.Notifications.Add(notification);
        var attempts = message.AttemptCount + 1;
        _db.NotificationDeliveries.Add(Delivery(notification.Id, DeliveryChannel.InApp, preference?.InAppEnabled != false, now, attempts));
        _db.NotificationDeliveries.Add(Delivery(notification.Id, DeliveryChannel.Email, sendEmail, now, attempts));
        _logger.LogInformation("Delivered notification {NotificationId} on {Channel}", notification.Id, DeliveryChannel.InApp);
    }

    private async Task QueueRemindersAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var soon = now.AddHours(24);
        var shifts = await (
            from assignment in _db.ShiftAssignments.AsNoTracking()
            join shift in _db.Shifts.AsNoTracking() on assignment.ShiftId equals shift.Id
            join profile in _db.ClinicianProfiles.AsNoTracking() on assignment.ClinicianProfileId equals profile.Id
            where assignment.AssignmentStatus == Domain.Scheduling.AssignmentStatus.Confirmed
                && shift.Status != Domain.Scheduling.ShiftScheduleStatus.Cancelled
                && shift.StartDateTime >= now && shift.StartDateTime <= soon
            select new { assignment.Id, profile.UserId, shift.StartDateTime, shift.Specialty }).ToListAsync(cancellationToken);
        foreach (var shift in shifts)
        {
            Add(shift.UserId, "ShiftReminder", "Upcoming shift", $"{shift.Specialty} starts at {shift.StartDateTime:u}.", "ShiftAssignment", shift.Id, $"ShiftReminder:{shift.Id}");
        }

        var expiration = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var credentials = await (
            from credential in _db.ClinicianCredentials.AsNoTracking()
            join profile in _db.ClinicianProfiles.AsNoTracking() on credential.ClinicianProfileId equals profile.Id
            where credential.ExpirationDate != null && credential.ExpirationDate <= expiration
            select new { credential.Id, credential.CredentialName, credential.ExpirationDate, profile.UserId }).ToListAsync(cancellationToken);
        foreach (var credential in credentials)
        {
            var expired = credential.ExpirationDate < today;
            Add(credential.UserId, expired ? "CredentialExpired" : "CredentialExpiring", expired ? "Credential expired" : "Credential expiring", $"{credential.CredentialName} {(expired ? "expired" : "expires")} on {credential.ExpirationDate:yyyy-MM-dd}.", "Credential", credential.Id, $"CredentialExpiration:{credential.Id}:{credential.ExpirationDate:yyyy-MM-dd}");
        }
    }

    private void Add(string userId, string type, string title, string message, string entityType, Guid entityId, string key)
    {
        if (_db.NotificationOutbox.Local.Any(item => item.IdempotencyKey == key) || _db.NotificationOutbox.Any(item => item.IdempotencyKey == key))
        {
            return;
        }

        _db.NotificationOutbox.Add(new NotificationOutbox
        {
            Id = Guid.NewGuid(),
            IdempotencyKey = key,
            RecipientUserId = userId,
            NotificationType = type,
            Title = title,
            Message = message,
            RelatedEntityType = entityType,
            RelatedEntityId = entityId,
            CreatedAt = DateTimeOffset.UtcNow,
            AvailableAt = DateTimeOffset.UtcNow
        });
    }

    private static bool Allows(NotificationPreference? preference, string type)
    {
        if (preference is null)
        {
            return true;
        }

        if (!preference.InAppEnabled && !preference.EmailEnabled)
        {
            return false;
        }

        if (type.StartsWith("Shift", StringComparison.Ordinal) && !preference.ShiftRemindersEnabled)
        {
            return false;
        }

        if ((type.StartsWith("Application", StringComparison.Ordinal) || type is "InterviewScheduled" or "OfferCreated" or "PlacementConfirmed") && !preference.ApplicationUpdatesEnabled)
        {
            return false;
        }

        return !type.StartsWith("Timesheet", StringComparison.Ordinal) || preference.TimesheetUpdatesEnabled;
    }

    private static NotificationDelivery Delivery(Guid notificationId, DeliveryChannel channel, bool deliver, DateTimeOffset now, int attempts) => new()
    {
        Id = Guid.NewGuid(),
        NotificationId = notificationId,
        Channel = channel,
        DeliveryStatus = deliver ? DeliveryStatus.Delivered : DeliveryStatus.Skipped,
        AttemptCount = attempts,
        LastAttemptAt = now,
        DeliveredAt = deliver ? now : null
    };
}
