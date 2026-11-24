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

/// <summary>`GET /public/company` : la marque de l'espace, lue sans session (pas de raison sociale Pro).</summary>
public sealed record GetPublicCompanyQuery : IRequest<CompanySettingsDto>;

internal sealed class GetPublicCompanyHandler(IAppDbContext db, IConfiguration configuration) : IRequestHandler<GetPublicCompanyQuery, CompanySettingsDto>
{
    public async Task<CompanySettingsDto> Handle(GetPublicCompanyQuery request, CancellationToken cancellationToken)
    {
        var company = await PublicCompany.CurrentAsync(db, cancellationToken);
        var label = (configuration["APP_ENV"] ?? "production") switch
        {
            "development" => "DEV",
            "recette" => "RECETTE",
            "uat" => "UAT",
            _ => null,
        };
        return CompanySettingsDto.From(company) with
        {
            ProLegalName = null,
            ProTaxId = null,
            Extra = new Dictionary<string, object?> { ["environmentLabel"] = label },
        };
    }
}
