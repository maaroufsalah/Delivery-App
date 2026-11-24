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

/// <summary>`POST /parcels/labels` : la planche d'une sélection, rendue tout de suite (500 au plus).</summary>
public sealed record PrintLabelSelectionCommand : LabelSelection, IRequest<FileContent>;

public sealed class PrintLabelSelectionValidator : LabelSelectionValidator<PrintLabelSelectionCommand>;

internal sealed class PrintLabelSelectionHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IFileStorage storage, IPdfService pdf) : IRequestHandler<PrintLabelSelectionCommand, FileContent>
{
    public async Task<FileContent> Handle(PrintLabelSelectionCommand request, CancellationToken cancellationToken)
    {
        var codes = await LabelSelections.CodesAsync(db, caller, clock, request, cancellationToken);
        if (codes.Count > ParcelLabels.AsyncThreshold)
        {
            throw BusinessException.Validation($"Plus de {ParcelLabels.AsyncThreshold} étiquettes : elles se préparent en arrière-plan.",
                new { reason = "labels-async", count = codes.Count, threshold = ParcelLabels.AsyncThreshold });
        }
        var parcels = ParcelLabels.InOrder(await ParcelLabels.LoadAsync(ParcelFilters.Scoped(db.Parcels.AsNoTracking(), caller).Where(p => codes.Contains(p.Code)), cancellationToken), codes);
        if (parcels.Count == 0) throw BusinessException.NotFound("Aucun de ces colis ne vous est accessible.");
        var sheet = await ParcelLabels.SheetAsync(db, storage, parcels, request.Format ?? LabelSheet.Format10X10, cancellationToken);
        return ParcelLabels.Pdf(ParcelLabels.Render(pdf, sheet).Content, parcels.Count == 1 ? $"{parcels[0].Code}.pdf" : $"etiquettes-{parcels.Count}.pdf");
    }
}
