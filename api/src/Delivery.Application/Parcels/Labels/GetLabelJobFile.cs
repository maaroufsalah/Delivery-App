using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Parcels.Labels;

/// <summary>`GET /parcels/label-jobs/:id/file` : le PDF (ou le ZIP) prêt.</summary>
public sealed record GetLabelJobFileQuery(string Id) : IRequest<FileContent>;

internal sealed class GetLabelJobFileHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IFileStorage storage) : IRequestHandler<GetLabelJobFileQuery, FileContent>
{
    public async Task<FileContent> Handle(GetLabelJobFileQuery request, CancellationToken cancellationToken)
    {
        var row = await LabelJobFiles.OwnAsync(db, caller, clock, request.Id, cancellationToken);
        if (row.Status != LabelJobStatus.READY || row.StorageKey is null)
        {
            throw BusinessException.Conflict("Les étiquettes ne sont pas encore prêtes.", new { reason = "label-job-not-ready", status = row.Status.ToString().ToLowerInvariant() });
        }
        var zip = row.StorageKey.EndsWith(".zip", StringComparison.Ordinal);
        var content = await storage.GetAsync(row.StorageKey, cancellationToken);
        return new FileContent(content, zip ? "application/zip" : FileContent.Pdf, row.Format == ParcelLabels.StockJobFormat ? $"situation-stock-livreur-{row.ParcelCount}.pdf" : $"etiquettes-{row.ParcelCount}.{(zip ? "zip" : "pdf")}") { CacheControl = "private, no-store" };
    }
}
