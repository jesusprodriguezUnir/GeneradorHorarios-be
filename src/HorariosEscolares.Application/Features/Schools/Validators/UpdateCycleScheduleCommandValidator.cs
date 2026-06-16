using FluentValidation;

namespace HorariosEscolares.Application.Features.Schools.Validators;

public sealed class UpdateCycleScheduleCommandValidator : AbstractValidator<UpdateCycleScheduleCommand>
{
    public UpdateCycleScheduleCommandValidator()
    {
        RuleFor(x => x.MorningStart).Must(BeValidTime).WithMessage("El formato de MorningStart no es válido (HH:mm).");
        RuleFor(x => x.MorningEnd).Must(BeValidTime).WithMessage("El formato de MorningEnd no es válido (HH:mm).");

        RuleFor(x => x.AfternoonStart)
            .Must(BeValidTime)
            .When(x => x.AfternoonStart is not null)
            .WithMessage("El formato de AfternoonStart no es válido (HH:mm).");

        RuleFor(x => x.AfternoonEnd)
            .Must(BeValidTime)
            .When(x => x.AfternoonEnd is not null)
            .WithMessage("El formato de AfternoonEnd no es válido (HH:mm).");
    }

    private static bool BeValidTime(string? value) => value is not null && TimeOnly.TryParse(value, out _);
}
