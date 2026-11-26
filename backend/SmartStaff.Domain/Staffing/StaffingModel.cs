namespace SmartStaff.Domain.Staffing;

public enum FacilityStatus
{
    Active,
    Inactive
}

public enum FacilityType
{
    Hospital,
    Clinic,
    LongTermCare,
    Outpatient,
    Other
}

public enum MembershipRole
{
    Manager,
    Recruiter
}

public enum EmploymentType
{
    FullTime,
    PartTime,
    Contract,
    PerDiem
}

public enum ShiftType
{
    Day,
    Night,
    Evening,
    Rotating,
    Weekend
}

public enum RequisitionStatus
{
    Draft,
    Open,
    Published,
    OnHold,
    Filled,
    Closed,
    Cancelled
}

public sealed class HealthcareFacility
{
    public Guid Id { get; set; }

    public string Name { get; set; } = "";

    public FacilityType FacilityType { get; set; }

    public string? Description { get; set; }

    public string Address { get; set; } = "";

    public string City { get; set; } = "";

    public string State { get; set; } = "";

    public string ZipCode { get; set; } = "";

    public string ContactEmail { get; set; } = "";

    public string ContactPhone { get; set; } = "";

    public FacilityStatus Status { get; set; } = FacilityStatus.Active;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public List<FacilityLocation> Locations { get; set; } = [];

    public List<FacilityMembership> Memberships { get; set; } = [];

    public List<JobRequisition> Requisitions { get; set; } = [];
}

public sealed class FacilityLocation
{
    public Guid Id { get; set; }

    public Guid FacilityId { get; set; }

    public HealthcareFacility Facility { get; set; } = null!;

    public string LocationName { get; set; } = "";

    public string Address { get; set; } = "";

    public string City { get; set; } = "";

    public string State { get; set; } = "";

    public string ZipCode { get; set; } = "";

    public bool IsActive { get; set; } = true;
}

public sealed class FacilityMembership
{
    public Guid Id { get; set; }

    public Guid FacilityId { get; set; }

    public HealthcareFacility Facility { get; set; } = null!;

    public string UserId { get; set; } = "";

    public MembershipRole MembershipRole { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class JobRequisition
{
    public Guid Id { get; set; }

    public Guid FacilityId { get; set; }

    public HealthcareFacility Facility { get; set; } = null!;

    public Guid LocationId { get; set; }

    public FacilityLocation Location { get; set; } = null!;

    public string JobTitle { get; set; } = "";

    public string Description { get; set; } = "";

    public string Specialty { get; set; } = "";

    public EmploymentType EmploymentType { get; set; }

    public ShiftType ShiftType { get; set; }

    public int RequiredExperience { get; set; }

    public string RequiredCertifications { get; set; } = "";

    public int OpenPositions { get; set; }

    public decimal PayRateMin { get; set; }

    public decimal PayRateMax { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public RequisitionStatus Status { get; set; } = RequisitionStatus.Draft;

    public string CreatedBy { get; set; } = "";

    public string? AssignedRecruiterId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
