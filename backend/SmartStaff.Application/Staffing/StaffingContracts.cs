namespace SmartStaff.Application.Staffing;

public sealed record FacilityRequest(
    string Name,
    string FacilityType,
    string? Description,
    string Address,
    string City,
    string State,
    string ZipCode,
    string ContactEmail,
    string ContactPhone,
    string? Status,
    string? TimeZoneId = null);

public sealed record LocationRequest(
    string LocationName,
    string Address,
    string City,
    string State,
    string ZipCode,
    bool? IsActive);

public sealed record MembershipRequest(string UserId, string MembershipRole);

public sealed record RequisitionRequest(
    Guid FacilityId,
    Guid LocationId,
    string JobTitle,
    string Description,
    string Specialty,
    string EmploymentType,
    string ShiftType,
    int RequiredExperience,
    string RequiredCertifications,
    int OpenPositions,
    decimal PayRateMin,
    decimal PayRateMax,
    DateOnly? StartDate,
    DateOnly? EndDate);

public sealed record RequisitionStatusRequest(string Status);

public sealed record AssignRecruiterRequest(string RecruiterId);

public sealed record FacilityListQuery(
    string? Search,
    string? Status,
    string? City,
    string? State,
    string? Sort,
    int Page = 1,
    int PageSize = 10);

public sealed record RequisitionListQuery(
    string? Search,
    string? Status,
    Guid? FacilityId,
    string? Specialty,
    string? City,
    string? State,
    string? ShiftType,
    string? EmploymentType,
    bool? AssignedToMe,
    string? Sort,
    int Page = 1,
    int PageSize = 10);

public sealed record FacilityDto(
    Guid Id,
    string Name,
    string FacilityType,
    string? Description,
    string Address,
    string City,
    string State,
    string ZipCode,
    string ContactEmail,
    string ContactPhone,
    string Status,
    string TimeZoneId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record LocationDto(
    Guid Id,
    Guid FacilityId,
    string LocationName,
    string Address,
    string City,
    string State,
    string ZipCode,
    bool IsActive);

public sealed record MembershipDto(
    Guid Id,
    Guid FacilityId,
    string UserId,
    string MembershipRole,
    DateTimeOffset CreatedAt);

public sealed record RequisitionDto(
    Guid Id,
    Guid FacilityId,
    string FacilityName,
    Guid LocationId,
    string LocationName,
    string City,
    string State,
    string JobTitle,
    string Description,
    string Specialty,
    string EmploymentType,
    string ShiftType,
    int RequiredExperience,
    string RequiredCertifications,
    int OpenPositions,
    decimal PayRateMin,
    decimal PayRateMax,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string Status,
    string CreatedBy,
    string? AssignedRecruiterId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record PageDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
