using FluentValidation.Results;

namespace HorariosEscolares.Application.Common.Exceptions;

public class ValidationException(IReadOnlyList<ValidationFailure> failures) : Exception
{
    public IReadOnlyList<ValidationFailure> Failures { get; } = failures;
}
