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

    public const string ExpectedInt = "Invalid input: expected int, received number";

    /// <summary>`.int()` : un nombre à virgule est refusé.</summary>
    public static IRuleBuilderOptions<T, double?> Int<T>(this IRuleBuilder<T, double?> rule) =>
        rule.Must(v => v is null || (Math.Floor(v.Value) == v.Value && !double.IsInfinity(v.Value))).WithMessage(ExpectedInt);

    public static IRuleBuilderOptions<T, double?> Min<T>(this IRuleBuilder<T, double?> rule, double min, string? message = null) =>
        rule.Must(v => v is null || v.Value >= min).WithMessage(message ?? TooSmallNumber((decimal)min));

    public static IRuleBuilderOptions<T, double?> Max<T>(this IRuleBuilder<T, double?> rule, double max, string? message = null) =>
        rule.Must(v => v is null || v.Value <= max).WithMessage(message ?? TooBigNumber((decimal)max));

    /// <summary>`madAmountSchema` : nombre requis (« required »), 0 à 1 000 000 MAD.</summary>
    public static void MadAmount<T>(this IRuleBuilder<T, double?> rule, bool nullable = false)
    {
        if (!nullable) rule.Must(v => v is not null).WithMessage("required");
        rule.Must(v => v is null || v.Value >= 0).WithMessage("negative-amount");
        rule.Must(v => v is null || v.Value <= 1_000_000).WithMessage("amount-too-large");
    }

    /// <summary>`madAmount0` (finance) : nombre requis, 0 à 1 000 000 MAD (« negative » sous zéro).</summary>
    public static void MadAmount0<T>(this IRuleBuilder<T, double?> rule)
    {
        rule.Must(v => v is not null).WithMessage("required");
        rule.Must(v => v is null || v.Value >= 0).WithMessage("negative");
        rule.Must(v => v is null || v.Value <= 1_000_000).WithMessage("amount-too-large");
    }

    /// <summary>Le type JSON reçu, tel que zod le nomme dans « expected X, received Y ».</summary>
    public static string Received(System.Text.Json.JsonElement? value) => value?.ValueKind switch
    {
        null or System.Text.Json.JsonValueKind.Undefined => "undefined",
        System.Text.Json.JsonValueKind.Null => "null",
        System.Text.Json.JsonValueKind.String => "string",
        System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False => "boolean",
        System.Text.Json.JsonValueKind.Array => "array",
        System.Text.Json.JsonValueKind.Object => "object",
        _ => "number",
    };

    /// <summary>`z.string().trim()` puis la normalisation des champs facultatifs vides.</summary>
    public static string? Trim(string? value) => value?.Trim();

    public static string? TrimLower(string? value) => value?.Trim().ToLowerInvariant();

    /// <summary>`value || null` : la chaîne vide devient <c>null</c>.</summary>
    public static string? EmptyToNull(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
