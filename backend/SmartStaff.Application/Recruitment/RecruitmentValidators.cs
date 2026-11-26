using FluentValidation;

namespace SmartStaff.Application.Recruitment;

public sealed class ClinicianProfileRequestValidator : AbstractValidator<ClinicianProfileRequest>
{
    public ClinicianProfileRequestValidator()
    {
        RuleFor(request => request.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(request => request.LastName).NotEmpty().MaximumLength(100);
        RuleFor(request => request.PhoneNumber).NotEmpty().MaximumLength(32);
        RuleFor(request => request.ProfessionalTitle).NotEmpty().MaximumLength(120);
        RuleFor(request => request.Specialty).NotEmpty().MaximumLength(120);
        RuleFor(request => request.YearsOfExperience).InclusiveBetween(0, 60);
        RuleFor(request => request.PreferredLocations).MaximumLength(500);
        RuleFor(request => request.EmploymentPreference).Must(value => Enum.TryParse<Domain.Recruitment.EmploymentPreference>(value, true, out _));
    }
}

public sealed class CredentialRequestValidator : AbstractValidator<CredentialRequest>
{
    public CredentialRequestValidator()
    {
        RuleFor(request => request.CredentialType).Must(value => Enum.TryParse<Domain.Recruitment.CredentialType>(value, true, out _));
        RuleFor(request => request.CredentialName).NotEmpty().MaximumLength(160);
        RuleFor(request => request.IssuingOrganization).NotEmpty().MaximumLength(160);
        RuleFor(request => request.CredentialNumber).NotEmpty().MaximumLength(80);
        RuleFor(request => request.ExpirationDate)
            .Must((request, expiration) => expiration is null || expiration >= request.IssueDate)
            .WithMessage("The expiration date must be on or after the issue date.");
    }
}

public sealed class CredentialVerificationRequestValidator : AbstractValidator<CredentialVerificationRequest>
{
    public CredentialVerificationRequestValidator()
    {
        RuleFor(request => request.VerificationStatus).Must(value => value is "Verified" or "Rejected" or "Pending");
    }
}

public sealed class ApplicationRequestValidator : AbstractValidator<ApplicationRequest>
{
    public ApplicationRequestValidator()
    {
        RuleFor(request => request.JobRequisitionId).NotEmpty();
    }
}

public sealed class ApplicationStatusRequestValidator : AbstractValidator<ApplicationStatusRequest>
{
    public ApplicationStatusRequestValidator()
    {
        RuleFor(request => request.Status).Must(value => Enum.TryParse<Domain.Recruitment.ApplicationStatus>(value, true, out _));
        RuleFor(request => request.Reason).MaximumLength(500);
        RuleFor(request => request.Notes).MaximumLength(2000);
    }
}

public sealed class PlacementRequestValidator : AbstractValidator<PlacementRequest>
{
    public PlacementRequestValidator()
    {
        RuleFor(request => request.JobApplicationId).NotEmpty();
        RuleFor(request => request.AgreedPayRate).GreaterThan(0);
        RuleFor(request => request.EndDate)
            .Must((request, end) => end is null || end >= request.StartDate)
            .WithMessage("The placement end date must be on or after the start date.");
    }
}

public sealed class PlacementStatusRequestValidator : AbstractValidator<PlacementStatusRequest>
{
    public PlacementStatusRequestValidator()
    {
        RuleFor(request => request.Status).Must(value => Enum.TryParse<Domain.Recruitment.PlacementStatus>(value, true, out _));
    }
}
