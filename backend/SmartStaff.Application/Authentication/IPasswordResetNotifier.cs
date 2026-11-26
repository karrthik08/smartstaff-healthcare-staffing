namespace SmartStaff.Application.Authentication;

public interface IPasswordResetNotifier
{
    Task NotifyAsync(string email, string resetToken, DateTimeOffset expiresAt, CancellationToken cancellationToken);
}
