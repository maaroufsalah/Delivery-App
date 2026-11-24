using System.Text.Json;
using System.Text.RegularExpressions;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;

namespace Delivery.Application.Claims;

/// <summary>`claimTypeSchema` : la même saisie pour créer et modifier un type.</summary>
public abstract partial record ClaimTypeInput
{
    public string? Code { get; init => field = Zod.Trim(value); }
    public string? Label { get; init => field = Zod.Trim(value); }
    public string? SubjectType { get; init; }
    public JsonElement? SortOrder { get; init; }
    public bool? IsActive { get; init; }

    public int SortOrderValue => SortOrder is null ? 0 : (int)JsCoerce.Number(SortOrder);

    [GeneratedRegex("^[a-z0-9-]+$")]
    public static partial Regex CodePattern();

    public void ApplyTo(ClaimType row)
    {
        row.Label = Label!;
        row.SubjectType = Enum.Parse<ClaimSubjectType>(Money.FromWire(SubjectType ?? "parcel"));
        row.SortOrder = SortOrderValue;
        row.IsActive = IsActive ?? true;
    }
}

public abstract class ClaimTypeInputValidator<T> : ZodValidator<T> where T : ClaimTypeInput
{
    private static readonly string[] SubjectTypes = ["parcel", "general"];

    protected ClaimTypeInputValidator()
    {
        RuleFor(x => x.Code).Required().MinLength(2, "too-short").MaxLength(40).Pattern(ClaimTypeInput.CodePattern(), "invalid-code");
        RuleFor(x => x.Label).Required().MinLength(2, "too-short").MaxLength(120);
        RuleFor(x => x.SubjectType).OneOf(SubjectTypes);
        RuleFor(x => x.SortOrder).Custom((value, context) =>
        {
            if (value is null) return;
            var issue = JsCoerce.NumberIssue(JsCoerce.Number(value), 0, 999, Zod.TooSmallNumber(0), Zod.TooBigNumber(999));
            if (issue is not null) context.AddFailure("sortOrder", issue);
        });
    }
}
