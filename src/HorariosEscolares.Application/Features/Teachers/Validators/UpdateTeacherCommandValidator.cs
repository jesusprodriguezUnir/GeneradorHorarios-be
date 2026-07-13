using FluentValidation;

namespace HorariosEscolares.Application.Features.Teachers.Validators;

public sealed class UpdateTeacherCommandValidator : AbstractValidator<UpdateTeacherCommand>
{
    public UpdateTeacherCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.FullName)
            .NotEmpty()
            .When(x => x.FullName is not null)
            .MaximumLength(200);

        RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => x.Email is not null)
            .MaximumLength(200);

        RuleFor(x => x.TeacherType)
            .NotEmpty()
            .When(x => x.TeacherType is not null)
            .MaximumLength(50);

        RuleFor(x => x.MaxWeeklyHours)
            .GreaterThan(0)
            .When(x => x.MaxWeeklyHours.HasValue);

        RuleFor(x => x.ColorKey).MaximumLength(50);

        RuleForEach(x => x.SubjectHours).ChildRules(sh =>
        {
            sh.RuleFor(x => x.SubjectKey).NotEmpty().MaximumLength(50);
            sh.RuleFor(x => x.WeeklyHours).GreaterThanOrEqualTo(0);
        });
    }
}
