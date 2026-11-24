using Delivery.Domain.Common;
using FluentValidation;
using MediatR;

namespace Delivery.Application.Common.Behaviors;

/// <summary>
/// Valide la requête avant le handler ; un échec devient le 422
/// `{ code: VALIDATION_FAILED, message: "Saisie invalide.", details: { issues } }`
/// du `ZodValidationPipe` de Speedy.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next(cancellationToken);
        }

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken)));
        var failures = results.SelectMany(r => r.Errors).Where(f => f is not null).ToList();
        if (failures.Count > 0)
        {
            throw BusinessException.Validation("Saisie invalide.", new
            {
                issues = failures.Select(f => new { path = ValidationPaths.ToPath(f.PropertyName), message = f.ErrorMessage }).ToList(),
            });
        }

        return await next(cancellationToken);
    }
}

public static class ValidationPaths
{
    /// <summary>`Items[0].Name` devient `items.0.name`, comme `issue.path.join('.')`.</summary>
    /// <summary>Préfixe d'un chemin déjà au format zod (clés de dictionnaire, corps brut) : rendu tel quel.</summary>
    public const string Raw = "@";

    public static string ToPath(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName)) return string.Empty;
        if (propertyName.StartsWith(Raw, StringComparison.Ordinal)) return propertyName[Raw.Length..];
        var parts = propertyName.Replace("[", ".", StringComparison.Ordinal).Replace("]", string.Empty, StringComparison.Ordinal).Split('.');
        return string.Join('.', parts.Where(p => p.Length > 0).Select(p => char.ToLowerInvariant(p[0]) + p[1..]));
    }
}
