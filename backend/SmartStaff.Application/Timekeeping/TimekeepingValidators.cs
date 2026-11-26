using FluentValidation;

namespace SmartStaff.Application.Timekeeping;

public sealed class TimesheetRequestValidator : AbstractValidator<TimesheetRequest>
{
    public TimesheetRequestValidator()
    {
        RuleFor(request => request.FacilityId).NotEmpty();
        RuleFor(request => request.PeriodEnd).GreaterThan(request => request.PeriodStart);
        RuleFor(request => request).Must(request => request.PeriodEnd.DayNumber - request.PeriodStart.DayNumber <= 31)
            .WithMessage("A timesheet period cannot be longer than 31 days.");
    }
}

public sealed class TimesheetEntryRequestValidator : AbstractValidator<TimesheetEntryRequest>
{
    public TimesheetEntryRequestValidator()
    {
        RuleFor(request => request.ShiftAssignmentId).NotEmpty();
        RuleFor(request => request.EndTime).GreaterThan(request => request.StartTime);
        RuleFor(request => request.BreakMinutes).GreaterThanOrEqualTo(0).LessThan(24 * 60);
        RuleFor(request => request.Notes).MaximumLength(500);
    }
}

public sealed class RejectTimesheetRequestValidator : AbstractValidator<RejectTimesheetRequest>
{
    public RejectTimesheetRequestValidator()
    {
        RuleFor(request => request.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class AdjustmentRequestValidator : AbstractValidator<AdjustmentRequest>
{
    public AdjustmentRequestValidator()
    {
        RuleFor(request => request.TimesheetEntryId).NotEmpty();
        RuleFor(request => request.AdjustedHours).GreaterThanOrEqualTo(0).LessThanOrEqualTo(24);
        RuleFor(request => request.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class OvertimePolicyRequestValidator : AbstractValidator<OvertimePolicyRequest>
{
    public OvertimePolicyRequestValidator()
    {
        RuleFor(request => request.JurisdictionCode).NotEmpty().Length(2, 16);
        RuleFor(request => request.RegularHoursPerDay).GreaterThan(0).LessThanOrEqualTo(24);
        RuleFor(request => request.RegularHoursPerPeriod).GreaterThan(0).LessThanOrEqualTo(200);
    }
}
