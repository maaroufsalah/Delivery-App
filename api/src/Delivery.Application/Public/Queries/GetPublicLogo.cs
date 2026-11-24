using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Settings;
using Delivery.Application.Settings.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Enums;
using Delivery.Domain.Pricing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Delivery.Application.Public.Queries;

/// <summary>`GET /public/logo/:fileName` : le logo ou l'icône courants de la société.</summary>
public sealed record GetPublicLogoQuery(string FileName) : IRequest<FileContent>;

internal sealed class GetPublicLogoHandler(IAppDbContext db, IFileStorage storage) : IRequestHandler<GetPublicLogoQuery, FileContent>
{
    public async Task<FileContent> Handle(GetPublicLogoQuery request, CancellationToken cancellationToken)
    {
        var company = await PublicCompany.CurrentAsync(db, cancellationToken);
        var key = new[] { Logos.KeyOf(company.Id, company.LogoUrl), Logos.KeyOf(company.Id, company.IconUrl) }
            .FirstOrDefault(k => k is not null && k.EndsWith("/" + request.FileName, StringComparison.Ordinal))
            ?? throw BusinessException.NotFound("Logo introuvable.");
        var content = await storage.GetAsync(key, cancellationToken);
        return new FileContent(content, Logos.MimeOf(request.FileName), request.FileName, Inline: true, CacheControl: "public, max-age=31536000, immutable");
    }
}
