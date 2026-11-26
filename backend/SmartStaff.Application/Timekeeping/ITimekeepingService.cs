using SmartStaff.Application.Common;

namespace SmartStaff.Application.Timekeeping;

public interface ITimekeepingService
{
    Task<OperationResult<AttendanceDto>> ClockInAsync(Guid assignmentId, ClockRequest request, string userId, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<AttendanceDto>> ClockOutAsync(Guid assignmentId, ClockRequest request, string userId, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<IReadOnlyList<AttendanceDto>>> ListAttendanceAsync(string userId, CancellationToken cancellationToken);

    Task<OperationResult<TimesheetDto>> CreateTimesheetAsync(TimesheetRequest request, string userId, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<TimesheetPageDto>> ListTimesheetsAsync(TimesheetListQuery query, string userId, CancellationToken cancellationToken);

    Task<OperationResult<TimesheetDto>> GetTimesheetAsync(Guid id, string userId, CancellationToken cancellationToken);

    Task<OperationResult<TimesheetDto>> AddEntryAsync(Guid timesheetId, TimesheetEntryRequest request, string userId, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<TimesheetDto>> UpdateEntryAsync(Guid timesheetId, Guid entryId, TimesheetEntryRequest request, string userId, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<TimesheetDto>> SubmitAsync(Guid id, string userId, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<TimesheetDto>> ApproveAsync(Guid id, string userId, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<TimesheetDto>> RejectAsync(Guid id, RejectTimesheetRequest request, string userId, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<IReadOnlyList<ApprovalHistoryDto>>> HistoryAsync(Guid id, string userId, CancellationToken cancellationToken);

    Task<OperationResult<TimesheetSummaryDto>> SummaryAsync(string userId, CancellationToken cancellationToken);

    Task<OperationResult<AdjustmentDto>> RequestAdjustmentAsync(AdjustmentRequest request, string userId, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<AdjustmentDto>> ReviewAdjustmentAsync(Guid id, ReviewAdjustmentRequest request, string userId, string? ipAddress, CancellationToken cancellationToken);

    Task<OperationResult<OvertimePolicyDto>> SavePolicyAsync(OvertimePolicyRequest request, string userId, string? ipAddress, CancellationToken cancellationToken);
}
