using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using Delivery.Domain.Parcels;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.CourierStock;

/// <summary>`GET /courier-stock`.</summary>
public sealed record GetCourierStockQuery(ListQueryParams Query, string[]? AgencyId, string? All) : IRequest<CourierStockPageDto>
{
    public CourierStockListFilter Filter => CourierStocks.ListFilter(Query, AgencyId, All);
}

internal sealed class GetCourierStockHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetCourierStockQuery, CourierStockPageDto>
{
    public async Task<CourierStockPageDto> Handle(GetCourierStockQuery request, CancellationToken cancellationToken)
    {
        var filter = request.Filter;
        var (rows, totals) = await CourierStocks.SnapshotAsync(db, caller, filter, cancellationToken);
        var start = (filter.Page - 1) * filter.PageSize;
        return new CourierStockPageDto(rows.Skip(start).Take(filter.PageSize).ToList(), rows.Count, filter.Page, filter.PageSize, totals);
    }
}
