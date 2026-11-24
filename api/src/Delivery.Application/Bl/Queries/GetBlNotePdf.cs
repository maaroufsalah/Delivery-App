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

/// <summary>`GET /bl/notes/:code/pdf` : le bon imprimé par un vendeur dit « Livreur &lt;nom court&gt; ».</summary>
public sealed record GetBlNotePdfQuery(string Code) : IRequest<FileContent>;

internal sealed class GetBlNotePdfHandler(IAppDbContext db, ICurrentUser caller, IPdfService pdf) : IRequestHandler<GetBlNotePdfQuery, FileContent>
{
    public async Task<FileContent> Handle(GetBlNotePdfQuery request, CancellationToken cancellationToken)
    {
        var note = await CourierMasks.ForReaderAsync(db, caller, await BlRows.NoteDetailAsync(db, caller, request.Code, cancellationToken), cancellationToken);
        var company = await PdfCompany.LoadAsync(db, "Bon de retour BL", cancellationToken);
        var sheet = new BlNoteSheet(company, note.Code, note.Status, note.AgencyName, note.CreatedAt, note.ClientName, note.CourierName, note.ReceivedAt, note.HandedOverAt, note.ReturnedToName,
            note.SlipCount, note.Items.Select(i => new BlNoteSheetLine(i.ParcelCode, i.RecipientName, i.Reference)).ToList());
        return new FileContent(pdf.Render(sheet), FileContent.Pdf, $"{note.Code}.pdf", CacheControl: "private, no-store");
    }
}
