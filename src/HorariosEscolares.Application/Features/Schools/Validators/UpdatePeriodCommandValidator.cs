using FluentValidation;

namespace HorariosEscolares.Application.Features.Schools.Validators;

public sealed class UpdatePeriodCommandValidator : AbstractValidator<UpdatePeriodCommand>
{
    public UpdatePeriodCommandValidator()
    {
        RuleFor(x => x.Months)
            .Must(BeValidMonths)
            .When(x => x.Months is not null)
            .WithMessage("Months debe contener al menos un mes, valores entre 1 y 12 y sin duplicados.");

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

    private static bool BeValidMonths(IReadOnlyList<int>? months)
        => months is not null
           && months.Count > 0
           && months.All(m => m is >= 1 and <= 12)
           && months.Distinct().Count() == months.Count;
}
