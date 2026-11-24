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

/// <summary>`POST /settings/theme/restore/:which` : revenir au standard ou à l'état précédent.</summary>
public sealed record RestoreThemeCommand(string Which) : IRequest<CompanySettingsDto>;

internal sealed class RestoreThemeHandler(IAppDbContext db, ICurrentUser caller, ILogger<RestoreThemeHandler> logger) : IRequestHandler<RestoreThemeCommand, CompanySettingsDto>
{
    public async Task<CompanySettingsDto> Handle(RestoreThemeCommand request, CancellationToken cancellationToken)
    {
        if (!caller.IsSuperAdmin()) throw BusinessException.Forbidden("Le thème avancé est réservé au super-administrateur.");
        if (request.Which is not ("standard" or "previous")) throw BusinessException.Validation($"Instantané inconnu : {request.Which}");
        var key = request.Which == "standard" ? SettingKeys.ThemeStandard : SettingKeys.ThemePrevious;
        var snapshot = await db.SettingAsync(key, cancellationToken);
        if (snapshot is null)
        {
            throw BusinessException.NotFound(request.Which == "standard" ? "Aucun standard enregistré : rejouez le seed." : "Aucune modification à annuler.");
        }
        var node = JsonNode.Parse(snapshot.Value.GetRawText())!;
        var primary = node["primaryColor"]?.GetValue<string>();
        var secondary = node["secondaryColor"]?.GetValue<string>();
        var company = await db.CompanyAsync(cancellationToken);
        await ThemeSnapshots.RememberPreviousAsync(db, company, primary, secondary, node["theme"] ?? new JsonObject(), logger, cancellationToken);
        company.PrimaryColor = primary ?? company.PrimaryColor;
        company.SecondaryColor = secondary ?? company.SecondaryColor;
        company.Theme = ThemeOverrides.Prune(node["theme"]).ToJsonString();
        await db.SaveChangesAsync(cancellationToken);
        return CompanySettingsDto.From(company);
    }
}
