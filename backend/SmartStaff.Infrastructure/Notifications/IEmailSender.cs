namespace SmartStaff.Infrastructure.Notifications;

public sealed record EmailMessage(string RecipientUserId, string Subject, string Body);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
