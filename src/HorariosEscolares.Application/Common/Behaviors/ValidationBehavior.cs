using FluentValidation;
using MediatR;

namespace HorariosEscolares.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);
            var failures = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, ct)));
            var errors = failures.SelectMany(x => x.Errors).Where(x => x is not null).ToList();
            if (errors.Count != 0)
                throw new Exceptions.ValidationException(errors);
        }
        return await next();
    }
}
