namespace SmartStaff.Application.Timekeeping;

public sealed record ClockRequest(DateTimeOffset? At);

public sealed record TimesheetRequest(Guid FacilityId, DateOnly PeriodStart, DateOnly PeriodEnd);

public sealed record TimesheetEntryRequest(
    Guid ShiftAssignmentId,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    int BreakMinutes,
    string? Notes);

public sealed record RejectTimesheetRequest(string Reason);

public sealed record AdjustmentRequest(Guid TimesheetEntryId, decimal AdjustedHours, string Reason);

public sealed record ReviewAdjustmentRequest(string Status);

public sealed record OvertimePolicyRequest(string JurisdictionCode, decimal? DailyOvertimeThreshold, decimal WeeklyOvertimeThreshold, string WorkweekStartsOn);

public sealed record TimesheetListQuery(Guid? FacilityId, string? Status, int Page, int PageSize);

public sealed record AttendanceDto(
    Guid Id,
    Guid ShiftAssignmentId,
    Guid ClinicianProfileId,
    string ClinicianName,
    Guid FacilityId,
    string TimeZoneId,
    DateTimeOffset ClockInTime,
    DateTimeOffset? ClockOutTime,
    string ClockInLocal,
    string? ClockOutLocal,
    string AttendanceStatus,
    bool Created);

public sealed record TimesheetEntryDto(
    Guid Id,
    Guid ShiftAssignmentId,
    DateOnly WorkDate,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string StartLocal,
    string EndLocal,
    int BreakMinutes,
    decimal RegularHours,
    decimal OvertimeHours,
    string Notes);

public sealed record TimesheetDto(
    Guid Id,
    Guid ClinicianProfileId,
    string ClinicianName,
    Guid FacilityId,
    string FacilityName,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal TotalRegularHours,
    decimal TotalOvertimeHours,
    string Status,
    DateTimeOffset? SubmittedAt,
    string? ApprovedBy,
    DateTimeOffset? ApprovedAt,
    string? RejectionReason,
    bool Created,
    IReadOnlyList<TimesheetEntryDto> Entries);

public sealed record ApprovalHistoryDto(
    Guid Id,
    string PreviousStatus,
    string NewStatus,
    string ChangedBy,
    DateTimeOffset ChangedAt,
    string Comments);

public sealed record AdjustmentDto(
    Guid Id,
    Guid TimesheetEntryId,
    decimal OriginalHours,
    decimal AdjustedHours,
    string Reason,
    string Status,
    string? ReviewedBy,
    DateTimeOffset? ReviewedAt);

public sealed record TimesheetSummaryDto(
    int Draft,
    int Submitted,
    int Approved,
    int Rejected,
    int OpenAttendance,
    int MissingTimesheets);

public sealed record OvertimePolicyDto(
    Guid Id,
    string JurisdictionCode,
    decimal? DailyOvertimeThreshold,
    decimal WeeklyOvertimeThreshold,
    string WorkweekStartsOn,
    bool IsActive);

public sealed record TimesheetPageDto(
    IReadOnlyList<TimesheetDto> Items,
    int Page,
    int PageSize,
    int Total);
