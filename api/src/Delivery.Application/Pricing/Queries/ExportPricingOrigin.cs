using AutoMapper;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Pricing.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Pricing.Queries;

/// <summary>`GET /pricing-rules/by-origin/:originCityId/export` : `tarifs-<code>.xlsx`.</summary>
public sealed record ExportPricingOriginQuery(string OriginCityId) : IRequest<FileContent>;

internal sealed class ExportPricingOriginHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel) : IRequestHandler<ExportPricingOriginQuery, FileContent>
{
    public async Task<FileContent> Handle(ExportPricingOriginQuery request, CancellationToken cancellationToken)
    {
        caller.AssertCanSeeInternalPricing("Règles de tarification");
        var page = await PricingGrid.ByOriginAsync(db, request.OriginCityId, new ListQuery { Page = 1, PageSize = 100_000, Sort = "name" }, null, cancellationToken);
        var rows = page.Items.Select(r => (IReadOnlyList<object?>)[r.Name, r.Code, r.Rate is null ? string.Empty : r.Rate.Value / 100m, r.DeliveryDelay ?? string.Empty]).ToList();
        var content = excel.Write($"Tarifs {page.Origin.Code}", ["Ville", "Code", "Tarif", "Délai"], rows, null);
        return new FileContent(content, FileContent.Xlsx, $"tarifs-{page.Origin.Code}.xlsx");
    }
}
