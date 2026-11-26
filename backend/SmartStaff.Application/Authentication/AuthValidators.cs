using FluentValidation;

namespace SmartStaff.Application.Authentication;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(request => request.Password).NotEmpty().MinimumLength(12).MaximumLength(128);
        RuleFor(request => request.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(request => request.LastName).NotEmpty().MaximumLength(100);
        RuleFor(request => request.PhoneNumber).MaximumLength(32);
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress();
        RuleFor(request => request.Password).NotEmpty();
    }
}

public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress();
    }
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress();
        RuleFor(request => request.Token).NotEmpty().MaximumLength(4096);
        RuleFor(request => request.NewPassword).NotEmpty().MinimumLength(12).MaximumLength(128);
    }
}

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(request => request.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(request => request.LastName).NotEmpty().MaximumLength(100);
        RuleFor(request => request.PhoneNumber).MaximumLength(32);
    }
}

public sealed class UpdateRolesRequestValidator : AbstractValidator<UpdateRolesRequest>
{
    public UpdateRolesRequestValidator()
    {
        RuleFor(request => request.Roles).NotNull().NotEmpty();
        RuleForEach(request => request.Roles).Must(AppRoles.IsAssignable)
            .WithMessage("One or more roles are not recognized.");
    }
}

public sealed class UpdateAccountStatusRequestValidator : AbstractValidator<UpdateAccountStatusRequest>
{
    public UpdateAccountStatusRequestValidator()
    {
        RuleFor(request => request.Active).NotNull();
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(request => request.CurrentPassword).NotEmpty().MaximumLength(128);
        RuleFor(request => request.NewPassword).NotEmpty().MinimumLength(12).MaximumLength(128);
    }
}
