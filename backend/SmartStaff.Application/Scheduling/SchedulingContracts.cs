namespace SmartStaff.Application.Scheduling;

public sealed record ShiftRequest(
    Guid FacilityId,
    Guid FacilityLocationId,
    Guid JobRequisitionId,
    DateTime StartLocal,
    DateTime EndLocal,
    int RequiredClinicians,
    string Specialty);

public sealed record AssignShiftRequest(Guid ClinicianProfileId);

public sealed record AvailabilityRequest(
    DateTimeOffset AvailableFrom,
    DateTimeOffset AvailableUntil,
    string AvailabilityType,
    string? Notes);

public sealed record ShiftChangeRequestBody(Guid ShiftAssignmentId, string RequestType, string Reason);

public sealed record ReviewShiftChangeRequest(string Status);

public sealed record ShiftListQuery(
    Guid? FacilityId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Status,
    string? View,
    int Page = 1,
    int PageSize = 20);

public sealed record ShiftDto(
    Guid Id,
    Guid FacilityId,
    string FacilityName,
    string TimeZoneId,
    Guid FacilityLocationId,
    string LocationName,
    Guid JobRequisitionId,
    DateTimeOffset StartDateTime,
    DateTimeOffset EndDateTime,
    string StartLocal,
    string EndLocal,
    int RequiredClinicians,
    int AssignedClinicians,
    int ConfirmedClinicians,
    string Specialty,
    string Status);

public sealed record AssignmentDto(
    Guid Id,
    Guid ShiftId,
    Guid ClinicianProfileId,
    string ClinicianName,
    Guid PlacementId,
    string AssignmentStatus,
    DateTimeOffset AssignedAt,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? CancelledAt);

public sealed record AvailabilityDto(
    Guid Id,
    Guid ClinicianProfileId,
    DateTimeOffset AvailableFrom,
    DateTimeOffset AvailableUntil,
    string AvailabilityType,
    string Notes);

public sealed record ShiftChangeDto(
    Guid Id,
    Guid ShiftAssignmentId,
    Guid ShiftId,
    string RequestType,
    string Reason,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReviewedAt);
