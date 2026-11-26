namespace SmartStaff.Domain.Timekeeping;

public enum AttendanceStatus
{
    Open,
    Completed
}

public enum TimesheetStatus
{
    Draft,
    Submitted,
    Approved,
    Rejected
}

public enum AdjustmentStatus
{
    Pending,
    Approved,
    Rejected
}

public sealed class OvertimePolicy
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string JurisdictionCode { get; set; } = "";

    public decimal RegularHoursPerDay { get; set; }

    public decimal RegularHoursPerPeriod { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AttendanceRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ShiftAssignmentId { get; set; }

    public Guid ClinicianProfileId { get; set; }

    public DateTimeOffset ClockInTime { get; set; }

    public DateTimeOffset? ClockOutTime { get; set; }

    public AttendanceStatus AttendanceStatus { get; set; } = AttendanceStatus.Open;

    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Timesheet
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ClinicianProfileId { get; set; }

    public Guid FacilityId { get; set; }

    public DateOnly PeriodStart { get; set; }

    public DateOnly PeriodEnd { get; set; }

    public decimal TotalRegularHours { get; set; }

    public decimal TotalOvertimeHours { get; set; }

    public TimesheetStatus Status { get; set; } = TimesheetStatus.Draft;

    public DateTimeOffset? SubmittedAt { get; set; }

    public string? ApprovedBy { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    public string? RejectionReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TimesheetEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TimesheetId { get; set; }

    public Guid ShiftAssignmentId { get; set; }

    public DateOnly WorkDate { get; set; }

    public DateTimeOffset StartTime { get; set; }

    public DateTimeOffset EndTime { get; set; }

    public int BreakMinutes { get; set; }

    public decimal RegularHours { get; set; }

    public decimal OvertimeHours { get; set; }

    public string Notes { get; set; } = "";
}

public sealed class TimesheetApprovalHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TimesheetId { get; set; }

    public TimesheetStatus PreviousStatus { get; set; }

    public TimesheetStatus NewStatus { get; set; }

    public string ChangedBy { get; set; } = "";

    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;

    public string Comments { get; set; } = "";
}

public sealed class TimesheetAdjustment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TimesheetEntryId { get; set; }

    public string RequestedBy { get; set; } = "";

    public decimal OriginalHours { get; set; }

    public decimal AdjustedHours { get; set; }

    public string Reason { get; set; } = "";

    public AdjustmentStatus Status { get; set; } = AdjustmentStatus.Pending;

    public string? ReviewedBy { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }
}
