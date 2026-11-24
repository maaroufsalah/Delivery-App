using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Bl;

/// <summary>`GET /bl/notes/export` : classeur « Bons BL ».</summary>
public sealed record ExportBlNotesQuery(BlNoteFilter Filter) : IRequest<FileContent>;

internal sealed class ExportBlNotesHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportBlNotesQuery, FileContent>
{
    private static readonly Dictionary<string, string> States = new()
    {
        ["draft"] = "Créé", ["awaiting-receipt"] = "En attente de réception", ["received"] = "Réceptionné", ["returned"] = "Remis au vendeur", ["closed"] = "Clôturé",
    };

    private static readonly IReadOnlyList<ExportColumn<BlNoteListItemDto>> Columns =
    [
        new("Bon BL", r => r.Code, Text: true),
        new("État", r => States[r.Status]),
        new("Client Pro", r => r.ClientName),
        new("Livreur", r => r.CourierName),
        new("Agence", r => r.AgencyName),
        new("BL", r => r.SlipCount),
        new("Remis à l’agence le", r => Exports.DateTimeText(r.HandedOverAt)),
        new("Réceptionné le", r => Exports.DateTimeText(r.ReceivedAt)),
        new("Remis au vendeur le", r => Exports.DateTimeText(r.ReturnedAt)),
        new("Remis à", r => r.ReturnedToName ?? string.Empty),
        new("Clôturé le", r => Exports.DateTimeText(r.ClosedAt)),
        new("Créé le", r => Exports.DateTimeText(r.CreatedAt)),
        new("Créé par", r => r.CreatedByName ?? string.Empty),
    ];

    public async Task<FileContent> Handle(ExportBlNotesQuery request, CancellationToken cancellationToken)
    {
        BlNoteLists.Validate(request.Filter);
        var where = BlNoteLists.Filter(db, caller, request.Filter);
        Exports.AssertWithinLimit(await where.CountAsync(cancellationToken));
        return await Exports.ToXlsxAsync(db, excel, clock, caller, "Bons BL", Columns, await BlRows.ProjectNotes(where).ToListAsync(cancellationToken), cancellationToken);
    }
}
