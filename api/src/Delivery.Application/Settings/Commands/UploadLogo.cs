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

/// <summary>`POST /settings/logo` : logo assaini (512 px, PNG) et icône carrée régénérée.</summary>
public sealed record UploadLogoCommand : IRequest<CompanySettingsDto>
{
    public string? Name { get; init => field = Zod.Trim(value); }

    public string? MimeType { get; init; }

    public string? Content { get; init; }
}

public sealed class UploadLogoValidator : ZodValidator<UploadLogoCommand>
{
    private static readonly string[] Types = ["image/svg+xml", "image/png", "image/jpeg", "image/webp"];

    public UploadLogoValidator()
    {
        RuleFor(x => x.Name).Required().MinLength(1, "required").MaxLength(120);
        RuleFor(x => x.MimeType).Must(t => t is not null && Types.Contains(t)).WithMessage("unsupported-file-type");
        RuleFor(x => x.Content).Required().MinLength(1, "required");
    }
}

internal sealed class UploadLogoHandler(IAppDbContext db, IFileStorage storage) : IRequestHandler<UploadLogoCommand, CompanySettingsDto>
{
    public async Task<CompanySettingsDto> Handle(UploadLogoCommand input, CancellationToken cancellationToken)
    {
        byte[] content;
        try
        {
            content = Convert.FromBase64String(input.Content!);
        }
        catch (FormatException)
        {
            content = [];
        }
        if (content.Length == 0) throw BusinessException.Validation("Le fichier est vide.", new { reason = "logo-empty" });
        if (content.Length > Logos.MaxBytes)
        {
            throw BusinessException.Validation($"Le logo dépasse {Math.Round(Logos.MaxBytes / 1024d)} ko.", new { reason = "logo-too-large", maxBytes = Logos.MaxBytes, bytes = content.Length });
        }
        var logo = storage.SanitizeImage(content, input.MimeType!, 512);
        var company = await db.CompanyAsync(cancellationToken);
        var previousLogo = company.LogoUrl;
        var previousIcon = company.IconUrl;
        var stored = await storage.PutAsync($"logos/{company.Id}", logo.Content, logo.MimeType, cancellationToken);
        var icon = await storage.PutAsync($"logos/{company.Id}", storage.BrandIcon(logo.Content, company.LogoBackground), "image/png", cancellationToken);
        company.LogoUrl = Logos.UrlOf(stored);
        company.IconUrl = Logos.UrlOf(icon);
        await db.SaveChangesAsync(cancellationToken);
        foreach (var oldKey in new[] { Logos.KeyOf(company.Id, previousLogo), Logos.KeyOf(company.Id, previousIcon) })
        {
            if (oldKey is not null) await storage.RemoveAsync(oldKey, cancellationToken);
        }
        return CompanySettingsDto.From(company);
    }
}
