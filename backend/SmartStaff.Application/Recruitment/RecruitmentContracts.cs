namespace SmartStaff.Application.Recruitment;

public sealed record ClinicianProfileRequest(
    string FirstName,
    string LastName,
    string PhoneNumber,
    string ProfessionalTitle,
    string Specialty,
    int YearsOfExperience,
    string PreferredLocations,
    string EmploymentPreference,
    DateOnly? AvailabilityDate);

public sealed record CredentialRequest(
    string CredentialType,
    string CredentialName,
    string IssuingOrganization,
    string CredentialNumber,
    DateOnly IssueDate,
    DateOnly? ExpirationDate);

public sealed record CredentialVerificationRequest(string VerificationStatus);

public sealed record ApplicationRequest(Guid JobRequisitionId);

public sealed record ApplicationStatusRequest(string Status, string? Reason, string? Notes);

public sealed record PlacementRequest(
    Guid JobApplicationId,
    DateOnly StartDate,
    DateOnly? EndDate,
    decimal AgreedPayRate);

public sealed record PlacementStatusRequest(string Status);

public sealed record ClinicianListQuery(
    string? Search,
    string? Specialty,
    int? MinExperience,
    string? Location,
    DateOnly? AvailableBy,
    string? Sort,
    int Page = 1,
    int PageSize = 10);

public sealed record ApplicationListQuery(
    string? Search,
    string? Status,
    Guid? FacilityId,
    string? Specialty,
    string? Sort,
    int Page = 1,
    int PageSize = 10);

public sealed record PlacementListQuery(
    string? Status,
    Guid? FacilityId,
    string? Sort,
    int Page = 1,
    int PageSize = 10);

public sealed record CredentialDto(
    Guid Id,
    string CredentialType,
    string CredentialName,
    string IssuingOrganization,
    string? CredentialNumber,
    DateOnly IssueDate,
    DateOnly? ExpirationDate,
    string VerificationStatus,
    DateTimeOffset? VerifiedAt);

public sealed record ClinicianProfileDto(
    Guid Id,
    string UserId,
    string FirstName,
    string LastName,
    string PhoneNumber,
    string ProfessionalTitle,
    string Specialty,
    int YearsOfExperience,
    string PreferredLocations,
    string EmploymentPreference,
    DateOnly? AvailabilityDate,
    string ProfileStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<CredentialDto> Credentials);

public sealed record ApplicationHistoryDto(
    Guid Id,
    string? PreviousStatus,
    string NewStatus,
    DateTimeOffset ChangedAt,
    string Reason);

public sealed record ApplicationDto(
    Guid Id,
    Guid JobRequisitionId,
    string JobTitle,
    string Specialty,
    Guid FacilityId,
    string FacilityName,
    Guid ClinicianProfileId,
    string ClinicianName,
    string Status,
    string? RecruiterId,
    string Notes,
    DateTimeOffset AppliedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<ApplicationHistoryDto> History);

public sealed record PlacementDto(
    Guid Id,
    Guid JobApplicationId,
    Guid FacilityId,
    string FacilityName,
    Guid ClinicianProfileId,
    string ClinicianName,
    string JobTitle,
    DateOnly StartDate,
    DateOnly? EndDate,
    decimal AgreedPayRate,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record RecruitmentSummaryDto(
    int Applications,
    int ActivePlacements,
    int OpenRequisitions,
    IReadOnlyList<StatusCountDto> Pipeline);

public sealed record StatusCountDto(string Status, int Count);
