using FluentValidation;

namespace SmartStaff.Application.Scheduling;

public sealed class ShiftRequestValidator : AbstractValidator<ShiftRequest>
{
    public ShiftRequestValidator()
    {
        RuleFor(request => request.FacilityId).NotEmpty();
        RuleFor(request => request.FacilityLocationId).NotEmpty();
        RuleFor(request => request.JobRequisitionId).NotEmpty();
        RuleFor(request => request.RequiredClinicians).InclusiveBetween(1, 50);
        RuleFor(request => request.Specialty).NotEmpty().MaximumLength(120);
        RuleFor(request => request.EndLocal).GreaterThan(request => request.StartLocal)
            .WithMessage("The shift end must be after the start.");
        RuleFor(request => request).Must(request => request.EndLocal - request.StartLocal <= TimeSpan.FromHours(24))
            .WithMessage("A shift cannot be longer than 24 hours.");
    }
}

public sealed class AssignShiftRequestValidator : AbstractValidator<AssignShiftRequest>
{
    public AssignShiftRequestValidator()
    {
        RuleFor(request => request.ClinicianProfileId).NotEmpty();
    }
}

public sealed class AvailabilityRequestValidator : AbstractValidator<AvailabilityRequest>
{
    public AvailabilityRequestValidator()
    {
        RuleFor(request => request.AvailableUntil).GreaterThan(request => request.AvailableFrom)
            .WithMessage("Availability must end after it starts.");
        RuleFor(request => request.AvailabilityType).Must(value => value is "Available" or "Unavailable");
        RuleFor(request => request.Notes).MaximumLength(500);
    }
}

public sealed class ShiftChangeRequestValidator : AbstractValidator<ShiftChangeRequestBody>
{
    public ShiftChangeRequestValidator()
    {
        RuleFor(request => request.ShiftAssignmentId).NotEmpty();
        RuleFor(request => request.RequestType).Must(value => value is "Cancel" or "Reschedule");
        RuleFor(request => request.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class ReviewShiftChangeRequestValidator : AbstractValidator<ReviewShiftChangeRequest>
{
    public ReviewShiftChangeRequestValidator()
    {
        RuleFor(request => request.Status).Must(value => value is "Approved" or "Rejected");
    }
}
