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

/// <summary>`GET /courier-stock/:courierId`.</summary>
public sealed record GetCourierStockDetailQuery(string CourierId, ListQueryParams Query, string? StatusId, string[]? ClientId, string[]? CityId, string[]? RouteId) : IRequest<CourierStockDetailDto>
{
    public CourierStockParcelFilter Filter => CourierStocks.ParcelFilter(Query, StatusId, ClientId, CityId, RouteId);
}

internal sealed class GetCourierStockDetailHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetCourierStockDetailQuery, CourierStockDetailDto>
{
    public async Task<CourierStockDetailDto> Handle(GetCourierStockDetailQuery request, CancellationToken cancellationToken)
    {
        var filter = request.Filter;
        var selection = await CourierStocks.SelectionAsync(db, caller, request.CourierId, filter, cancellationToken);
        var start = (filter.Page - 1) * filter.PageSize;
        return new CourierStockDetailDto(selection.Courier, selection.Counters, selection.ByStatus,
            new CourierStockParcelPageDto(selection.Parcels.Skip(start).Take(filter.PageSize).Select(p => p.View).ToList(), selection.Parcels.Count, filter.Page, filter.PageSize),
            selection.Options);
    }
}
