using SmartStaff.Application.Common;

namespace SmartStaff.Application.Notifications;

public interface INotificationProcessor
{
    Task<int> ProcessAsync(CancellationToken cancellationToken);
}

public interface INotificationService
{
    Task<OperationResult<NotificationPageDto>> ListAsync(string userId, bool? unread, int page, int pageSize, CancellationToken cancellationToken);

    Task<OperationResult<int>> UnreadCountAsync(string userId, CancellationToken cancellationToken);

    Task<OperationResult<NotificationDto>> MarkReadAsync(Guid id, string userId, CancellationToken cancellationToken);

    Task<OperationResult<int>> MarkAllReadAsync(string userId, CancellationToken cancellationToken);

    Task<OperationResult<NotificationPreferenceDto>> GetPreferencesAsync(string userId, CancellationToken cancellationToken);

    Task<OperationResult<NotificationPreferenceDto>> SavePreferencesAsync(NotificationPreferenceDto request, string userId, CancellationToken cancellationToken);
}

public sealed record NotificationDto(
    Guid Id,
    string NotificationType,
    string Title,
    string Message,
    string RelatedEntityType,
    Guid? RelatedEntityId,
    bool IsRead,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);

public sealed record NotificationPageDto(IReadOnlyList<NotificationDto> Items, int Page, int PageSize, int Total, int UnreadCount);

public sealed record NotificationPreferenceDto(
    bool EmailEnabled,
    bool InAppEnabled,
    bool ShiftRemindersEnabled,
    bool ApplicationUpdatesEnabled,
    bool TimesheetUpdatesEnabled);
