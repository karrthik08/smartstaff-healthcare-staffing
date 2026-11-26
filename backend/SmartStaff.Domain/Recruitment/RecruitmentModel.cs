namespace SmartStaff.Domain.Recruitment;

public enum ProfileStatus
{
    Incomplete,
    Active,
    Inactive
}

public enum EmploymentPreference
{
    FullTime,
    PartTime,
    Contract,
    PerDiem,
    Any
}

public enum CredentialType
{
    License,
    Certification,
    Education
}

public enum VerificationStatus
{
    Pending,
    Verified,
    Rejected
}

public enum ApplicationStatus
{
    Applied,
    UnderReview,
    Screening,
    SubmittedToFacility,
    Interview,
    Offer,
    Placed,
    Rejected,
    Withdrawn
}

public enum PlacementStatus
{
    Pending,
    Active,
    Completed,
    Cancelled
}

public sealed class ClinicianProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = "";

    public string FirstName { get; set; } = "";

    public string LastName { get; set; } = "";

    public string PhoneNumber { get; set; } = "";

    public string ProfessionalTitle { get; set; } = "";

    public string Specialty { get; set; } = "";

    public int YearsOfExperience { get; set; }

    public string PreferredLocations { get; set; } = "";

    public EmploymentPreference EmploymentPreference { get; set; } = EmploymentPreference.Any;

    public DateOnly? AvailabilityDate { get; set; }

    public ProfileStatus ProfileStatus { get; set; } = ProfileStatus.Incomplete;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<ClinicianCredential> Credentials { get; set; } = [];
}

public sealed class ClinicianCredential
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ClinicianProfileId { get; set; }

    public ClinicianProfile Profile { get; set; } = null!;

    public CredentialType CredentialType { get; set; }

    public string CredentialName { get; set; } = "";

    public string IssuingOrganization { get; set; } = "";

    public string CredentialNumber { get; set; } = "";

    public DateOnly IssueDate { get; set; }

    public DateOnly? ExpirationDate { get; set; }

    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Pending;

    public string? VerifiedBy { get; set; }

    public DateTimeOffset? VerifiedAt { get; set; }
}

public sealed class JobApplication
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid JobRequisitionId { get; set; }

    public Guid ClinicianProfileId { get; set; }

    public ClinicianProfile Profile { get; set; } = null!;

    public DateTimeOffset AppliedAt { get; set; } = DateTimeOffset.UtcNow;

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Applied;

    public string? RecruiterId { get; set; }

    public string Notes { get; set; } = "";

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<ApplicationStatusHistory> History { get; set; } = [];
}

public sealed class ApplicationStatusHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid JobApplicationId { get; set; }

    public JobApplication Application { get; set; } = null!;

    public ApplicationStatus? PreviousStatus { get; set; }

    public ApplicationStatus NewStatus { get; set; }

    public string ChangedBy { get; set; } = "";

    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;

    public string Reason { get; set; } = "";
}

public sealed class Placement
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid JobApplicationId { get; set; }

    public JobApplication Application { get; set; } = null!;

    public Guid FacilityId { get; set; }

    public Guid ClinicianProfileId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public decimal AgreedPayRate { get; set; }

    public PlacementStatus Status { get; set; } = PlacementStatus.Active;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
