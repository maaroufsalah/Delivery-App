using System.Text.Json.Nodes;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.Settings.Dtos;
using Delivery.Domain.Common;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Settings.Commands;

/// <summary>`PATCH /settings/company` : les champs fournis remplacent les courants.</summary>
public sealed record UpdateCompanySettingsCommand(JsonNode? Body) : IRequest<CompanySettingsDto>;

public sealed class UpdateCompanySettingsValidator : ZodValidator<UpdateCompanySettingsCommand>
{
    public UpdateCompanySettingsValidator() => RuleFor(x => x).Custom((value, context) =>
    {
        var issues = new List<(string Path, string Message)>();
        CompanyPatch.Parse(value.Body, issues);
        foreach (var (path, message) in issues) context.AddFailure("@" + path, message);
    });
}

internal sealed class UpdateCompanySettingsHandler(IAppDbContext db, IFileStorage storage, ILogger<UpdateCompanySettingsHandler> logger)
    : IRequestHandler<UpdateCompanySettingsCommand, CompanySettingsDto>
{
    public async Task<CompanySettingsDto> Handle(UpdateCompanySettingsCommand request, CancellationToken cancellationToken)
    {
        var patch = CompanyPatch.Parse(request.Body, []);
        var company = await db.CompanyAsync(cancellationToken);
        var current = CompanySettingsDto.From(company);
        string? Text(string key, string? fallback) => patch.TryGetPropertyValue(key, out var node) ? node?.GetValue<string>() : fallback;
        var theme = patch.TryGetPropertyValue("theme", out var themeNode) ? themeNode : null;

        var primary = Text("primaryColor", current.PrimaryColor)!;
        var secondary = Text("secondaryColor", current.SecondaryColor)!;
        await ThemeSnapshots.RememberPreviousAsync(db, company, primary, secondary, theme, logger, cancellationToken);

        var logo = Text("logo", current.Logo)!;
        var nextBackground = Zod.EmptyToNull(Text("logoBackground", current.LogoBackground)?.Trim());
        var iconUrl = logo != current.Logo || nextBackground != current.LogoBackground
            ? await Logos.RefreshIconAsync(storage, company.Id, logo, current.Icon, nextBackground, cancellationToken)
            : current.Icon;

        company.Name = Text("name", current.Name)!;
        company.ShortName = Text("shortName", current.ShortName)!.Trim();
        company.LogoUrl = logo;
        company.IconUrl = iconUrl;
        company.LogoBackground = nextBackground;
        company.PrimaryColor = primary;
        company.SecondaryColor = secondary;
        company.Currency = Text("currency", current.Currency)!;
        company.Country = Text("country", current.Country)!;
        company.Timezone = Text("timeZone", current.TimeZone)!;
        company.Locales = patch["locales"] is JsonArray locales ? locales.Select(l => l!.GetValue<string>()).ToList() : [.. current.Locales];
        company.DefaultLocale = Text("defaultLocale", current.DefaultLocale)!;
        company.LegalName = Zod.EmptyToNull(Text("legalName", current.LegalName)?.Trim());
        company.TaxId = Zod.EmptyToNull(Text("taxId", current.TaxId)?.Trim());
        company.Rc = Zod.EmptyToNull(Text("rc", current.Rc)?.Trim());
        company.ProLegalName = Zod.EmptyToNull(Text("proLegalName", current.ProLegalName)?.Trim());
        company.ProTaxId = Zod.EmptyToNull(Text("proTaxId", current.ProTaxId)?.Trim());
        company.Address = Zod.EmptyToNull(Text("address", current.Address)?.Trim());
        company.City = Zod.EmptyToNull(Text("city", current.City)?.Trim());
        company.Phone = Zod.EmptyToNull(Text("phone", current.Phone)?.Trim());
        company.Email = Zod.EmptyToNull(Text("email", current.Email)?.Trim());
        company.Website = Zod.EmptyToNull(Text("website", current.Website)?.Trim());
        if (theme is not null) company.Theme = ThemeOverrides.Prune(theme).ToJsonString();
        await db.SaveChangesAsync(cancellationToken);
        return CompanySettingsDto.From(company);
    }
}
