using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartStaff.API.Authentication;
using SmartStaff.Application.Notifications;

namespace SmartStaff.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications)
    {
        _notifications = notifications;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool? unread, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await _notifications.ListAsync(UserId(), unread, page, pageSize, cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken cancellationToken)
    {
        var result = await _notifications.UnreadCountAsync(UserId(), cancellationToken);
        return result.Succeeded ? Ok(new { count = result.Value }) : this.FromFailure(result);
    }

    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        var result = await _notifications.MarkReadAsync(id, UserId(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var result = await _notifications.MarkAllReadAsync(UserId(), cancellationToken);
        return result.Succeeded ? Ok(new { updated = result.Value }) : this.FromFailure(result);
    }

    [HttpGet("preferences")]
    public async Task<IActionResult> Preferences(CancellationToken cancellationToken)
    {
        var result = await _notifications.GetPreferencesAsync(UserId(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    [HttpPut("preferences")]
    public async Task<IActionResult> SavePreferences(NotificationPreferenceDto request, CancellationToken cancellationToken)
    {
        var result = await _notifications.SavePreferencesAsync(request, UserId(), cancellationToken);
        return result.Succeeded ? Ok(result.Value) : this.FromFailure(result);
    }

    private string UserId() => User.CurrentUserId() ?? "";
}
