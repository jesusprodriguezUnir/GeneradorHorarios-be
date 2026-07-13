using FluentValidation;

namespace HorariosEscolares.Application.Features.Schools.Validators;

public sealed class UpdateStageCommandValidator : AbstractValidator<UpdateStageCommand>
{
    public UpdateStageCommandValidator()
    {
        RuleFor(x => x.MorningStart)
            .Must(BeValidTime)
            .When(x => x.MorningStart is not null)
            .WithMessage("El formato de MorningStart no es válido (HH:mm).");

        RuleFor(x => x.AfternoonStart)
            .Must(BeValidTime)
            .When(x => x.AfternoonStart is not null)
            .WithMessage("El formato de AfternoonStart no es válido (HH:mm).");

        RuleFor(x => x.WorkingDays)
            .Must(days => days is not null && days.Count > 0)
            .When(x => x.WorkingDays is not null)
            .WithMessage("Debe haber al menos un día lectivo.");

        RuleFor(x => x.WorkingDays)
            .Must(days => days is not null && days.Distinct().Count() == days.Count)
            .When(x => x.WorkingDays is not null)
            .WithMessage("Los días lectivos no pueden repetirse.");

        RuleFor(x => x.SlotMinutes)
            .GreaterThan(0)
            .When(x => x.SlotMinutes.HasValue);

        RuleFor(x => x.SlotsPerDay)
            .GreaterThan(0)
            .When(x => x.SlotsPerDay.HasValue);

        RuleFor(x => x.AfternoonSlots)
            .GreaterThanOrEqualTo(0)
            .When(x => x.AfternoonSlots.HasValue);
    }

    private static bool BeValidTime(string? value) => value is not null && TimeOnly.TryParse(value, out _);
}
