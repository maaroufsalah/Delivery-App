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

/// <summary>`PATCH /settings/theme` : le thème avancé, réservé au super-administrateur.</summary>
public sealed record UpdateThemeCommand(JsonNode? Body) : IRequest<CompanySettingsDto>;

public sealed class UpdateThemeValidator : ZodValidator<UpdateThemeCommand>
{
    public UpdateThemeValidator() => RuleFor(x => x).Custom((value, context) =>
    {
        var issues = new List<(string Path, string Message)>();
        ThemeOverrides.Parse(value.Body, string.Empty, issues);
        foreach (var (path, message) in issues) context.AddFailure("@" + path.TrimStart('.'), message);
    });
}

internal sealed class UpdateThemeHandler(IAppDbContext db, ICurrentUser caller, ILogger<UpdateThemeHandler> logger) : IRequestHandler<UpdateThemeCommand, CompanySettingsDto>
{
    public async Task<CompanySettingsDto> Handle(UpdateThemeCommand request, CancellationToken cancellationToken)
    {
        if (!caller.IsSuperAdmin()) throw BusinessException.Forbidden("Le thème avancé est réservé au super-administrateur.");
        var theme = ThemeOverrides.Parse(request.Body, string.Empty, []);
        var company = await db.CompanyAsync(cancellationToken);
        await ThemeSnapshots.RememberPreviousAsync(db, company, null, null, theme, logger, cancellationToken);
        company.Theme = ThemeOverrides.Prune(theme).ToJsonString();
        await db.SaveChangesAsync(cancellationToken);
        return CompanySettingsDto.From(company);
    }
}
