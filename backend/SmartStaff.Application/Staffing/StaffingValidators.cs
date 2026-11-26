using FluentValidation;
using SmartStaff.Domain.Staffing;

namespace SmartStaff.Application.Staffing;

public sealed class FacilityRequestValidator : AbstractValidator<FacilityRequest>
{
    public FacilityRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(200);
        RuleFor(request => request.FacilityType).NotEmpty().Must(value => Enum.TryParse<FacilityType>(value, true, out _)).WithMessage("Choose a valid facility type.");
        RuleFor(request => request.Description).MaximumLength(2000);
        RuleFor(request => request.Address).NotEmpty().MaximumLength(200);
        RuleFor(request => request.City).NotEmpty().MaximumLength(100);
        RuleFor(request => request.State).NotEmpty().Length(2);
        RuleFor(request => request.ZipCode).NotEmpty().Matches(@"^\d{5}(-\d{4})?$").WithMessage("Enter a 5-digit ZIP code.");
        RuleFor(request => request.ContactEmail).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(request => request.ContactPhone).NotEmpty().MaximumLength(32);
        RuleFor(request => request.Status)
            .Must(value => string.IsNullOrWhiteSpace(value) || Enum.TryParse<FacilityStatus>(value, true, out _))
            .WithMessage("Choose a valid facility status.");
    }
}

public sealed class LocationRequestValidator : AbstractValidator<LocationRequest>
{
    public LocationRequestValidator()
    {
        RuleFor(request => request.LocationName).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Address).NotEmpty().MaximumLength(200);
        RuleFor(request => request.City).NotEmpty().MaximumLength(100);
        RuleFor(request => request.State).NotEmpty().Length(2);
        RuleFor(request => request.ZipCode).NotEmpty().Matches(@"^\d{5}(-\d{4})?$");
    }
}

public sealed class MembershipRequestValidator : AbstractValidator<MembershipRequest>
{
    public MembershipRequestValidator()
    {
        RuleFor(request => request.UserId).NotEmpty().MaximumLength(450);
        RuleFor(request => request.MembershipRole).NotEmpty().Must(value => Enum.TryParse<MembershipRole>(value, true, out _));
    }
}

public sealed class RequisitionRequestValidator : AbstractValidator<RequisitionRequest>
{
    public RequisitionRequestValidator()
    {
        RuleFor(request => request.FacilityId).NotEmpty();
        RuleFor(request => request.LocationId).NotEmpty();
        RuleFor(request => request.JobTitle).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Description).NotEmpty().MaximumLength(4000);
        RuleFor(request => request.Specialty).NotEmpty().MaximumLength(120);
        RuleFor(request => request.EmploymentType).Must(value => Enum.TryParse<EmploymentType>(value, true, out _));
        RuleFor(request => request.ShiftType).Must(value => Enum.TryParse<ShiftType>(value, true, out _));
        RuleFor(request => request.RequiredExperience).InclusiveBetween(0, 80);
        RuleFor(request => request.RequiredCertifications).MaximumLength(500);
        RuleFor(request => request.OpenPositions).InclusiveBetween(1, 500);
        RuleFor(request => request.PayRateMin).GreaterThanOrEqualTo(0);
        RuleFor(request => request.PayRateMax).GreaterThanOrEqualTo(request => request.PayRateMin);
        RuleFor(request => request.EndDate)
            .Must((request, end) => end is null || request.StartDate is null || end >= request.StartDate)
            .WithMessage("The end date must be on or after the start date.");
    }
}

public sealed class RequisitionStatusRequestValidator : AbstractValidator<RequisitionStatusRequest>
{
    public RequisitionStatusRequestValidator()
    {
        RuleFor(request => request.Status).NotEmpty().Must(value => Enum.TryParse<RequisitionStatus>(value, true, out _));
    }
}

public sealed class AssignRecruiterRequestValidator : AbstractValidator<AssignRecruiterRequest>
{
    public AssignRecruiterRequestValidator()
    {
        RuleFor(request => request.RecruiterId).NotEmpty().MaximumLength(450);
    }
}
