using FluentValidation;

namespace HorariosEscolares.Application.Features.Schools.Validators;

public sealed class CreatePeriodCommandValidator : AbstractValidator<CreatePeriodCommand>
{
    public CreatePeriodCommandValidator()
    {
        RuleFor(x => x.Key).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Months)
            .Must(BeValidMonths)
            .WithMessage("Months debe contener al menos un mes, valores entre 1 y 12 y sin duplicados.");
        RuleFor(x => x.SlotMinutes).GreaterThan(0);
        RuleFor(x => x.SlotsPerDay).GreaterThan(0);
        RuleFor(x => x.AfternoonSlots).GreaterThanOrEqualTo(0);
    }

    private static bool BeValidMonths(IReadOnlyList<int> months)
        => months.Count > 0
           && months.All(m => m is >= 1 and <= 12)
           && months.Distinct().Count() == months.Count;
}
