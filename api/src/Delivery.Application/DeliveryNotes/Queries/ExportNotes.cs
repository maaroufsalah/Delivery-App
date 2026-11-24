using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.Parcels.Commands;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.DeliveryNotes;

/// <summary>`GET /delivery-notes/export` : classeur « Bons ».</summary>
public sealed record ExportNotesQuery(NoteListFilter Filter) : IRequest<FileContent>;

internal sealed class ExportNotesHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportNotesQuery, FileContent>
{
    private static readonly IReadOnlyList<ExportColumn<DeliveryNoteListItemDto>> Columns =
    [
        new("Bon", r => r.Code),
        new("Type", r => DocumentLabels.Enum(r.Kind)),
        new("Statut", r => DocumentLabels.Enum(r.Status)),
        new("E-commerçant", r => r.ClientName),
        new("Ville", r => r.CityName),
        new("Colis", r => r.ParcelCount),
        new("Ramassage offert", r => Exports.Bool(r.PickupWaived)),
        new("Créé le", r => Exports.DateTimeText(r.CreatedAt)),
        new("Validé le", r => Exports.DateTimeText(r.ValidatedAt)),
    ];

    public async Task<FileContent> Handle(ExportNotesQuery request, CancellationToken cancellationToken)
    {
        var policy = await NoteRules.PolicyAsync(db, caller.AgencyId, cancellationToken);
        var where = NoteRows.Filter(db.DeliveryNotes.AsNoTracking(), caller, request.Filter);
        Exports.AssertWithinLimit(await where.CountAsync(cancellationToken));
        var rows = (await NoteRows.ProjectAsync(where.OrderByDescending(n => n.CreatedAt), cancellationToken)).Select(r => NoteRows.Fill(new DeliveryNoteListItemDto(), r, policy)).ToList();
        return await Exports.ToXlsxAsync(db, excel, clock, "Bons", Columns, rows, cancellationToken);
    }
}
