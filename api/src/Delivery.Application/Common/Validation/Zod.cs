using System.Text.RegularExpressions;
using FluentValidation;
using FluentValidation.Results;

namespace Delivery.Application.Common.Validation;

/// <summary>
/// Validator qui reproduit l'ordre de zod v4 : les règles de champ d'abord ;
/// les raffinements d'objet (`refine`, `superRefine`) seulement si elles passent.
/// </summary>
public abstract class ZodValidator<T> : AbstractValidator<T>
{
    public override async Task<ValidationResult> ValidateAsync(ValidationContext<T> context, CancellationToken cancellation = default)
    {
        var result = await base.ValidateAsync(context, cancellation);
        if (result.IsValid && context.InstanceToValidate is not null)
        {
            var issues = new List<ValidationFailure>();
            Refine(context.InstanceToValidate, (path, message) => issues.Add(new ValidationFailure(path, message)));
            result.Errors.AddRange(issues);
        }
        return result;
    }

    public override ValidationResult Validate(ValidationContext<T> context)
    {
        var result = base.Validate(context);
        if (result.IsValid && context.InstanceToValidate is not null)
        {
            Refine(context.InstanceToValidate, (path, message) => result.Errors.Add(new ValidationFailure(path, message)));
        }
        return result;
    }

    /// <summary>Raffinements d'objet ; <c>issue(path, message)</c> pour chaque problème.</summary>
    protected virtual void Refine(T value, Action<string, string> issue)
    {
    }
}

/// <summary>Messages et règles de zod v4 (messages par défaut en anglais, clés métier sinon).</summary>
public static class Zod
{
    public const string EmailPattern = @"^(?!\.)(?!.*\.\.)([A-Za-z0-9_'+\-\.]*)[A-Za-z0-9_+-]@([A-Za-z0-9][A-Za-z0-9\-]*\.)+[A-Za-z]{2,}$";

    private static readonly Regex EmailRegex = new(EmailPattern, RegexOptions.Compiled);

    public static string Expected(string type, string received = "undefined") => $"Invalid input: expected {type}, received {received}";

    public static string TooSmallString(int min) => $"Too small: expected string to have >={min} characters";

    public static string TooBigString(int max) => $"Too big: expected string to have <={max} characters";

    public static string TooSmallArray(int min) => $"Too small: expected array to have >={min} items";

    public static string TooSmallNumber(decimal min) => $"Too small: expected number to be >={min}";

    public static string TooBigNumber(decimal max) => $"Too big: expected number to be <={max}";

    public static string InvalidOption(IEnumerable<string> values) => "Invalid option: expected one of " + string.Join("|", values.Select(v => $"\"{v}\""));

    public const string InvalidInput = "Invalid input";

    public const string InvalidEmail = "Invalid email address";

    public static bool IsEmail(string? value) => value is not null && EmailRegex.IsMatch(value);

    /// <summary>Champ requis : `undefined` ou `null` → invalid_type.</summary>
    public static IRuleBuilderOptions<T, TProperty> Required<T, TProperty>(this IRuleBuilder<T, TProperty> rule, string type = "string") =>
        rule.Must(v => v is not null).WithMessage(Expected(type));

    public static IRuleBuilderOptions<T, string?> MinLength<T>(this IRuleBuilder<T, string?> rule, int min, string? message = null) =>
        rule.Must(v => v is null || v.Length >= min).WithMessage(message ?? TooSmallString(min));

    public static IRuleBuilderOptions<T, string?> MaxLength<T>(this IRuleBuilder<T, string?> rule, int max, string? message = null) =>
        rule.Must(v => v is null || v.Length <= max).WithMessage(message ?? TooBigString(max));

    public static IRuleBuilderOptions<T, string?> EmailAddress<T>(this IRuleBuilder<T, string?> rule, string message = InvalidEmail) =>
        rule.Must(v => v is null || IsEmail(v)).WithMessage(message);

    public static IRuleBuilderOptions<T, string?> Pattern<T>(this IRuleBuilder<T, string?> rule, Regex regex, string message) =>
        rule.Must(v => v is null || regex.IsMatch(v)).WithMessage(message);

    public static IRuleBuilderOptions<T, string?> OneOf<T>(this IRuleBuilder<T, string?> rule, IReadOnlyCollection<string> values) =>
        rule.Must(v => v is null || values.Contains(v)).WithMessage(InvalidOption(values));

    /// <summary>`z.string().trim()` puis la normalisation des champs facultatifs vides.</summary>
    public static string? Trim(string? value) => value?.Trim();

    public static string? TrimLower(string? value) => value?.Trim().ToLowerInvariant();

    /// <summary>`value || null` : la chaîne vide devient <c>null</c>.</summary>
    public static string? EmptyToNull(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
