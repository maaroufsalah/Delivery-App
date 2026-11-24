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

/// <summary>`GET /courier-stock/pdf`.</summary>
public sealed record GetCourierStockPdfQuery(ListQueryParams Query, string[]? AgencyId, string? All) : IRequest<FileContent>;

internal sealed class GetCourierStockPdfHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IFileStorage storage, IPdfService pdf) : IRequestHandler<GetCourierStockPdfQuery, FileContent>
{
    public async Task<FileContent> Handle(GetCourierStockPdfQuery request, CancellationToken cancellationToken) =>
        CourierStockPdfs.RenderSync(pdf, await CourierStockPdfs.GlobalAsync(db, caller, clock, storage, CourierStocks.ListFilter(request.Query, request.AgencyId, request.All), cancellationToken));
}
