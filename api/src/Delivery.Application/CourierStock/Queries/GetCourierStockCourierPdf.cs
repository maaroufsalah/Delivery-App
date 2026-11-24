using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Application.Parcels.Labels;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.CourierStock;

/// <summary>`GET /courier-stock/:courierId/pdf`.</summary>
public sealed record GetCourierStockCourierPdfQuery(string CourierId, ListQueryParams Query, string? StatusId, string[]? ClientId, string[]? CityId, string[]? RouteId) : IRequest<FileContent>;

internal sealed class GetCourierStockCourierPdfHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IFileStorage storage, IPdfService pdf)
    : IRequestHandler<GetCourierStockCourierPdfQuery, FileContent>
{
    public async Task<FileContent> Handle(GetCourierStockCourierPdfQuery request, CancellationToken cancellationToken) =>
        CourierStockPdfs.RenderSync(pdf, await CourierStockPdfs.IndividualAsync(db, caller, clock, storage, request.CourierId,
            CourierStocks.ParcelFilter(request.Query, request.StatusId, request.ClientId, request.CityId, request.RouteId), cancellationToken));
}
