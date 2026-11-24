using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.Settings.Dtos;
using Delivery.Application.Settings.Queries;
using Delivery.Domain.Common;
using Delivery.Domain.Pricing;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Settings.Commands;

/// <summary>`PATCH /settings/business` : préfixe des colis, langues, réclamations, étiquette.</summary>
public sealed record UpdateBusinessSettingsCommand : IRequest<BusinessSettingsDto>
{
    public string? ParcelCodePrefix { get; init => field = value?.Trim().ToUpperInvariant(); }

    public IReadOnlyList<string>? LocalesEnabled { get; init; }

    public string? LocalesDefault { get; init; }

    public JsonElement? ClaimResolutionDays { get; init; }

    public string? LabelTemplate { get; init; }

    public string? LabelBrandColor { get; init => field = Zod.Trim(value); }

    public string? LabelBanner { get; init => field = Zod.Trim(value); }
}

public sealed class UpdateBusinessSettingsValidator : ZodValidator<UpdateBusinessSettingsCommand>
{
    private static readonly Regex Prefix = new("^[A-Z0-9-]*$", RegexOptions.Compiled);
    private static readonly string[] Templates = ["classic", "branded"];

    public UpdateBusinessSettingsValidator()
    {
        RuleFor(x => x.ParcelCodePrefix).Required().MaxLength(6, "too-large").Pattern(Prefix, "invalid-prefix");
        RuleFor(x => x.LocalesEnabled).Required("array").Must(l => l is null || l.Count >= 1).WithMessage("at-least-one-locale");
        RuleForEach(x => x.LocalesEnabled).Must(l => CompanyPatch.Locales.Contains(l)).WithMessage(Zod.InvalidOption(CompanyPatch.Locales));
        RuleFor(x => x.LocalesDefault).Must(l => l is not null && CompanyPatch.Locales.Contains(l)).WithMessage(Zod.InvalidOption(CompanyPatch.Locales));
        RuleFor(x => x.ClaimResolutionDays).Custom((value, context) =>
        {
            var issue = JsCoerce.NumberIssue(JsCoerce.Number(value), 0, 365, "negative-amount", "too-large");
            if (issue is not null) context.AddFailure("claimResolutionDays", issue);
        });
        RuleFor(x => x.LabelTemplate).Must(t => t is not null && Templates.Contains(t)).WithMessage(Zod.InvalidOption(Templates));
        RuleFor(x => x.LabelBrandColor).Required().Must(c => c is null || c.Length == 0 || Colors.IsHex(c)).WithMessage("invalid-color");
        RuleFor(x => x.LabelBanner).Required().MaxLength(200, "too-large");
    }
}

internal sealed class UpdateBusinessSettingsHandler(IAppDbContext db) : IRequestHandler<UpdateBusinessSettingsCommand, BusinessSettingsDto>
{
    public async Task<BusinessSettingsDto> Handle(UpdateBusinessSettingsCommand input, CancellationToken cancellationToken)
    {
        if (!input.LocalesEnabled!.Contains(input.LocalesDefault!))
        {
            throw BusinessException.Validation("La langue par défaut doit faire partie des langues activées.", new { field = "localesDefault" });
        }
        var company = await db.CompanyAsync(cancellationToken);
        await db.WriteChangedAsync(company.Id,
        [
            (SettingKeys.ParcelCodePrefix, input.ParcelCodePrefix, ""),
            (SettingKeys.LocalesEnabled, new JsonArray(input.LocalesEnabled!.Select(l => (JsonNode?)l).ToArray()), new JsonArray("fr")),
            (SettingKeys.LocalesDefault, input.LocalesDefault, "fr"),
            (SettingKeys.ClaimResolutionDays, JsCoerce.Number(input.ClaimResolutionDays), 0),
            (SettingKeys.LabelTemplate, input.LabelTemplate, "classic"),
            (SettingKeys.LabelBrandColor, input.LabelBrandColor, ""),
            (SettingKeys.LabelBanner, input.LabelBanner, ""),
        ], cancellationToken);
        return await GetBusinessSettingsHandler.BuildAsync(db, cancellationToken);
    }
}
