using FluentValidation;

namespace SmartStaff.Application.Configuration;

public sealed class DatabaseOptionsValidator : AbstractValidator<DatabaseOptions>
{
    public DatabaseOptionsValidator()
    {
        RuleFor(options => options.Host)
            .NotEmpty()
            .WithMessage("POSTGRES_HOST is required.");

        RuleFor(options => options.Port)
            .InclusiveBetween(1, 65535)
            .WithMessage("POSTGRES_PORT must be a number from 1 to 65535.");

        RuleFor(options => options.Name)
            .NotEmpty()
            .WithMessage("POSTGRES_DB is required.");

        RuleFor(options => options.Username)
            .NotEmpty()
            .WithMessage("POSTGRES_USER is required.");

        RuleFor(options => options.Password)
            .NotEmpty()
            .WithMessage("POSTGRES_PASSWORD environment variable is required.");
    }
}
