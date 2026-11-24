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

/// <summary>
/// `courierStockPdfJobSchema` (union sur `kind`) : « global » (filtres de la liste) ou « courier » (un livreur et ses
/// filtres). Le document est préparé tout de suite, rendu en arrière-plan (file des étiquettes).
/// </summary>
public sealed record CreateCourierStockPdfJobCommand(JsonElement? Body) : IRequest<LabelJobDto>;

internal sealed class CreateCourierStockPdfJobHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IFileStorage storage, LabelJobQueue queue)
    : IRequestHandler<CreateCourierStockPdfJobCommand, LabelJobDto>
{
    public async Task<LabelJobDto> Handle(CreateCourierStockPdfJobCommand request, CancellationToken cancellationToken)
    {
        var body = request.Body is { ValueKind: JsonValueKind.Object } b ? b : default;
        string? Text(string key) => body.ValueKind == JsonValueKind.Object && body.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        string[]? Many(string key) => body.ValueKind == JsonValueKind.Object && body.TryGetProperty(key, out var v)
            ? v.ValueKind == JsonValueKind.String ? [v.GetString()!] : v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray() : null
            : null;
        var kind = Text("kind");
        if (kind is not ("global" or "courier"))
        {
            throw BusinessException.Validation("Saisie invalide.", new { issues = new[] { new { path = "kind", message = "Invalid input" } } });
        }
        var query = new ListQueryParams { Search = Text("search"), Sort = Text("sort"), Order = Text("order") };
        CourierStockPdfs.Prepared prepared;
        if (kind == "global")
        {
            prepared = await CourierStockPdfs.GlobalAsync(db, caller, clock, storage, CourierStocks.ListFilter(query, Many("agencyId"), Text("all")), cancellationToken);
        }
        else
        {
            var courierId = Text("courierId")?.Trim();
            if (string.IsNullOrEmpty(courierId))
            {
                throw BusinessException.Validation("Saisie invalide.", new { issues = new[] { new { path = "courierId", message = courierId is null ? Zod.Expected("string") : Zod.TooSmallString(1) } } });
            }
            var status = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("statusId", out var s) ? s.ToString() : null;
            prepared = await CourierStockPdfs.IndividualAsync(db, caller, clock, storage, courierId,
                CourierStocks.ParcelFilter(query, status, Many("clientId"), Many("cityId"), Many("routeId")), cancellationToken);
        }
        var now = clock.UtcNow;
        var row = new LabelJob { RequestedById = caller.UserId, Format = ParcelLabels.StockJobFormat, ParcelCount = prepared.Rows, CreatedAt = now, ExpiresAt = now.AddHours(ParcelLabels.JobTtlHours) };
        db.LabelJobs.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        queue.Enqueue(new LabelJobWork(row.Id, [], row.Format, $"courier-stock/{caller.CompanyId}", prepared.Data));
        return LabelJobDto.From(row);
    }
}
