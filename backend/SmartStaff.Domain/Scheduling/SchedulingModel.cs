namespace SmartStaff.Domain.Scheduling;

public enum ShiftScheduleStatus
{
    Draft,
    Published,
    Cancelled
}

public enum AssignmentStatus
{
    Assigned,
    Confirmed,
    Cancelled
}

public enum AvailabilityType
{
    Available,
    Unavailable
}

public enum ShiftChangeType
{
    Cancel,
    Reschedule
}

public enum ShiftChangeStatus
{
    Pending,
    Approved,
    Rejected
}

public sealed class Shift
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FacilityId { get; set; }

    public Guid FacilityLocationId { get; set; }

    public Guid JobRequisitionId { get; set; }

    public DateTimeOffset StartDateTime { get; set; }

    public DateTimeOffset EndDateTime { get; set; }

    public int RequiredClinicians { get; set; }

    public string Specialty { get; set; } = "";

    public ShiftScheduleStatus Status { get; set; } = ShiftScheduleStatus.Draft;

    public string CreatedBy { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ShiftAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ShiftId { get; set; }

    public Guid ClinicianProfileId { get; set; }

    public Guid PlacementId { get; set; }

    public AssignmentStatus AssignmentStatus { get; set; } = AssignmentStatus.Assigned;

    public string AssignedBy { get; set; } = "";

    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ConfirmedAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }
}

public sealed class ClinicianAvailability
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ClinicianProfileId { get; set; }

    public DateTimeOffset AvailableFrom { get; set; }

    public DateTimeOffset AvailableUntil { get; set; }

    public AvailabilityType AvailabilityType { get; set; } = AvailabilityType.Available;

    public string Notes { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ShiftChangeRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ShiftAssignmentId { get; set; }

    public string RequestedBy { get; set; } = "";

    public ShiftChangeType RequestType { get; set; }

    public string Reason { get; set; } = "";

    public ShiftChangeStatus Status { get; set; } = ShiftChangeStatus.Pending;

    public string? ReviewedBy { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
