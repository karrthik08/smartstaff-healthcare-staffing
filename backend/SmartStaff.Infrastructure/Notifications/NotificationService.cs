using Microsoft.EntityFrameworkCore;
using SmartStaff.Application.Common;
using SmartStaff.Application.Notifications;
using SmartStaff.Domain.Notifications;
using SmartStaff.Infrastructure.Persistence;

namespace SmartStaff.Infrastructure.Notifications;

public sealed class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _db;

    public NotificationService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<OperationResult<NotificationPageDto>> ListAsync(string userId, bool? unread, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 50 ? 20 : pageSize;
        var query = _db.Notifications.AsNoTracking().Where(item => item.RecipientUserId == userId);
        if (unread == true)
        {
            query = query.Where(item => !item.IsRead);
        }

        var total = await query.CountAsync(cancellationToken);
        var unreadCount = await _db.Notifications.CountAsync(item => item.RecipientUserId == userId && !item.IsRead, cancellationToken);
        var items = await query.OrderByDescending(item => item.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(item => new NotificationDto(item.Id, item.NotificationType, item.Title, item.Message, item.RelatedEntityType, item.RelatedEntityId, item.IsRead, item.CreatedAt, item.ReadAt))
            .ToListAsync(cancellationToken);
        return OperationResult<NotificationPageDto>.Ok(new NotificationPageDto(items, page, pageSize, total, unreadCount));
    }

    public async Task<OperationResult<int>> UnreadCountAsync(string userId, CancellationToken cancellationToken) =>
        OperationResult<int>.Ok(await _db.Notifications.CountAsync(item => item.RecipientUserId == userId && !item.IsRead, cancellationToken));

    public async Task<OperationResult<NotificationDto>> MarkReadAsync(Guid id, string userId, CancellationToken cancellationToken)
    {
        var notification = await _db.Notifications.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (notification is null)
        {
            return OperationResult<NotificationDto>.Fail(404, "Not Found", "Notification not found.");
        }

        if (notification.RecipientUserId != userId)
        {
            return OperationResult<NotificationDto>.Fail(403, "Forbidden", "You can only read your own notifications.");
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return OperationResult<NotificationDto>.Ok(Map(notification));
    }

    public async Task<OperationResult<int>> MarkAllReadAsync(string userId, CancellationToken cancellationToken)
    {
        var unread = await _db.Notifications.Where(item => item.RecipientUserId == userId && !item.IsRead).ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        foreach (var notification in unread)
        {
            notification.IsRead = true;
            notification.ReadAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<int>.Ok(unread.Count);
    }

    public async Task<OperationResult<NotificationPreferenceDto>> GetPreferencesAsync(string userId, CancellationToken cancellationToken)
    {
        var preference = await _db.NotificationPreferences.AsNoTracking().FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        return OperationResult<NotificationPreferenceDto>.Ok(preference is null
            ? new NotificationPreferenceDto(true, true, true, true, true)
            : Map(preference));
    }

    public async Task<OperationResult<NotificationPreferenceDto>> SavePreferencesAsync(NotificationPreferenceDto request, string userId, CancellationToken cancellationToken)
    {
        var preference = await _db.NotificationPreferences.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (preference is null)
        {
            preference = new NotificationPreference { Id = Guid.NewGuid(), UserId = userId };
            _db.NotificationPreferences.Add(preference);
        }

        preference.EmailEnabled = request.EmailEnabled;
        preference.InAppEnabled = request.InAppEnabled;
        preference.ShiftRemindersEnabled = request.ShiftRemindersEnabled;
        preference.ApplicationUpdatesEnabled = request.ApplicationUpdatesEnabled;
        preference.TimesheetUpdatesEnabled = request.TimesheetUpdatesEnabled;
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<NotificationPreferenceDto>.Ok(Map(preference));
    }

    private static NotificationDto Map(Notification notification) => new(notification.Id, notification.NotificationType, notification.Title, notification.Message, notification.RelatedEntityType, notification.RelatedEntityId, notification.IsRead, notification.CreatedAt, notification.ReadAt);

    private static NotificationPreferenceDto Map(NotificationPreference preference) => new(preference.EmailEnabled, preference.InAppEnabled, preference.ShiftRemindersEnabled, preference.ApplicationUpdatesEnabled, preference.TimesheetUpdatesEnabled);
}
