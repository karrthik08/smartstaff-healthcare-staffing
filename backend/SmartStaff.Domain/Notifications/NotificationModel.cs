namespace SmartStaff.Domain.Notifications;

public enum DeliveryChannel
{
    InApp,
    Email
}

public enum DeliveryStatus
{
    Pending,
    Delivered,
    Failed,
    Skipped
}

public sealed class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string RecipientUserId { get; set; } = "";

    public string NotificationType { get; set; } = "";

    public string Title { get; set; } = "";

    public string Message { get; set; } = "";

    public string RelatedEntityType { get; set; } = "";

    public Guid? RelatedEntityId { get; set; }

    public string IdempotencyKey { get; set; } = "";

    public bool IsRead { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ReadAt { get; set; }
}

public sealed class NotificationPreference
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = "";

    public bool EmailEnabled { get; set; } = true;

    public bool InAppEnabled { get; set; } = true;

    public bool ShiftRemindersEnabled { get; set; } = true;

    public bool ApplicationUpdatesEnabled { get; set; } = true;

    public bool TimesheetUpdatesEnabled { get; set; } = true;
}

public sealed class NotificationDelivery
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid NotificationId { get; set; }

    public DeliveryChannel Channel { get; set; }

    public DeliveryStatus DeliveryStatus { get; set; } = DeliveryStatus.Pending;

    public int AttemptCount { get; set; }

    public DateTimeOffset? LastAttemptAt { get; set; }

    public DateTimeOffset? DeliveredAt { get; set; }

    public string? ErrorCode { get; set; }
}

public sealed class NotificationTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string TemplateKey { get; set; } = "";

    public string Subject { get; set; } = "";

    public string Body { get; set; } = "";

    public bool IsActive { get; set; } = true;
}

public sealed class NotificationOutbox
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string IdempotencyKey { get; set; } = "";

    public string RecipientUserId { get; set; } = "";

    public string NotificationType { get; set; } = "";

    public string Title { get; set; } = "";

    public string Message { get; set; } = "";

    public string RelatedEntityType { get; set; } = "";

    public Guid? RelatedEntityId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset AvailableAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ProcessedAt { get; set; }

    public int AttemptCount { get; set; }

    public string? LastError { get; set; }
}

public sealed class EmailDispatch
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string RecipientUserId { get; set; } = "";

    public string Subject { get; set; } = "";

    public string Body { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
