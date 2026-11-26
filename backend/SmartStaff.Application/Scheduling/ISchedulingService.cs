using SmartStaff.Application.Common;
using SmartStaff.Application.Staffing;

namespace SmartStaff.Application.Scheduling;

public interface ISchedulingService
{
    Task<OperationResult<ShiftDto>> CreateAsync(ShiftRequest request, string userId, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<ShiftDto>> UpdateAsync(Guid id, ShiftRequest request, string userId, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<ShiftDto>> PublishAsync(Guid id, string userId, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<ShiftDto>> CancelAsync(Guid id, string userId, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<PageDto<ShiftDto>>> ListAsync(ShiftListQuery query, string userId, CancellationToken cancellationToken);

    Task<OperationResult<ShiftDto>> GetAsync(Guid id, string userId, CancellationToken cancellationToken);

    Task<OperationResult<AssignmentDto>> AssignAsync(Guid shiftId, AssignShiftRequest request, string userId, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<AssignmentDto>> ConfirmAsync(Guid assignmentId, string userId, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<AssignmentDto>> CancelAssignmentAsync(Guid assignmentId, string userId, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<AvailabilityDto>> AddAvailabilityAsync(AvailabilityRequest request, string userId, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<IReadOnlyList<AvailabilityDto>>> ListAvailabilityAsync(string userId, CancellationToken cancellationToken);

    Task<OperationResult<ShiftChangeDto>> RequestChangeAsync(ShiftChangeRequestBody request, string userId, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<IReadOnlyList<ShiftChangeDto>>> ListChangesAsync(string userId, CancellationToken cancellationToken);

    Task<OperationResult<ShiftChangeDto>> ReviewChangeAsync(Guid id, ReviewShiftChangeRequest request, string userId, string? ipAddress, CancellationToken cancellationToken);
}
