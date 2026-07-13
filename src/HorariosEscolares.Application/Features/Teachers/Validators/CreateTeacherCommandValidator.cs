using FluentValidation;

namespace HorariosEscolares.Application.Features.Teachers.Validators;

public sealed class CreateTeacherCommandValidator : AbstractValidator<CreateTeacherCommand>
{
    public CreateTeacherCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.TeacherType).NotEmpty().MaximumLength(50);
        RuleFor(x => x.MaxWeeklyHours).GreaterThan(0);
        RuleFor(x => x.ColorKey).MaximumLength(50);

        RuleForEach(x => x.SubjectHours).ChildRules(sh =>
        {
            sh.RuleFor(x => x.SubjectKey).NotEmpty().MaximumLength(50);
            sh.RuleFor(x => x.WeeklyHours).GreaterThanOrEqualTo(0);
        });

        RuleForEach(x => x.StageAssignments).ChildRules(sa =>
        {
            sa.RuleFor(x => x.StageId).NotEmpty();
        });
    }
}
