using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Application.Parcels.Labels;
using Delivery.Domain.Common;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Routes;

/// <summary>`GET /delivery-notes/:code/pdf` : le bon de ramassage ou de dépôt.</summary>
public sealed record GetNotePdfQuery(string Code) : IRequest<FileContent>;

internal sealed class GetNotePdfHandler(IAppDbContext db, ICurrentUser caller, IPdfService pdf) : IRequestHandler<GetNotePdfQuery, FileContent>
{
    public async Task<FileContent> Handle(GetNotePdfQuery request, CancellationToken cancellationToken)
    {
        var note = await NoteRows.DetailAsync(db, caller, request.Code, cancellationToken);
        var company = await PdfCompany.LoadAsync(db, "Bon", cancellationToken);
        var agency = await db.Agencies.AsNoTracking().Where(a => a.Id == note.AgencyId).Select(a => a.Name).FirstOrDefaultAsync(cancellationToken);
        var sheet = new DeliveryNoteSheet(company, note.Code, note.Kind, note.ClientName, agency ?? "-", note.CityName, note.CreatedAt, note.ValidatedAt, note.PickupWaived,
            note.Parcels.Select(p => new DeliveryNoteSheetLine(p.Code, p.RecipientName, p.DestinationCityName, p.DestinationDistrictName, p.CodAmount, p.DeliveryFee, p.Quantity,
                NoteRows.ClientCorrectionText(p.ClientCorrection) is { Length: > 0 } text ? text : null)).ToList(),
            note.PickedCount, note.RemovedCount);
        return ParcelLabels.Pdf(pdf.Render(sheet), $"{note.Code}.pdf");
    }
}
