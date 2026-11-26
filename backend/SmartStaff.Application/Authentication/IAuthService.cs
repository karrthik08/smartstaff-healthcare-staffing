using SmartStaff.Application.Common;

namespace SmartStaff.Application.Authentication;

public interface IAuthService
{
    Task<OperationResult<AuthenticatedUserDto>> RegisterAsync(RegisterRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<AuthSession>> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<AuthSession>> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<bool>> LogoutAsync(string? refreshToken, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<bool>> ForgotPasswordAsync(ForgotPasswordRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<bool>> ResetPasswordAsync(ResetPasswordRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<AuthenticatedUserDto>> GetUserAsync(string userId, CancellationToken cancellationToken);
}

public interface IProfileService
{
    Task<OperationResult<AuthenticatedUserDto>> UpdateAsync(string userId, UpdateProfileRequest request, string? ipAddress, CancellationToken cancellationToken);
}

public interface IUserAdminService
{
    Task<OperationResult<IReadOnlyList<UserSummaryDto>>> ListUsersAsync(CancellationToken cancellationToken);

    Task<OperationResult<UserSummaryDto>> UpdateRolesAsync(string actorUserId, string targetUserId, UpdateRolesRequest request, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<IReadOnlyList<AuditLogDto>>> ListAuditLogsAsync(CancellationToken cancellationToken);
}
