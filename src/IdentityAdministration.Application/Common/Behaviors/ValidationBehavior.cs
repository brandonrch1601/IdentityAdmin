using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IdentityAdministration.Application.Common.Behaviors;

/// <summary>
/// Behavior de pipeline de MediatR que ejecuta todos los validadores de FluentValidation
/// registrados para el request antes de que llegue al handler.
/// Lanza <see cref="ValidationException"/> si hay fallos.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators,
    ILogger<ValidationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken) // CS: pipeline behavior contract
    {
        if (!validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
        {
            logger.LogWarning(
                "Validación fallida para {RequestType}: {Errores}",
                typeof(TRequest).Name,
                string.Join("; ", failures.Select(f => $"[{f.PropertyName}] {f.ErrorMessage}")));

            throw new ValidationException(failures);
        }

        return await next();
    }
}
