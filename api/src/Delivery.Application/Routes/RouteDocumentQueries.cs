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

/// <summary>`GET /routes/:code/sheet` : la feuille de route du circuit.</summary>
public sealed record GetRouteSheetQuery(string Code) : IRequest<FileContent>;

internal sealed class GetRouteSheetHandler(IAppDbContext db, ICurrentUser caller, IPdfService pdf) : IRequestHandler<GetRouteSheetQuery, FileContent>
{
    public async Task<FileContent> Handle(GetRouteSheetQuery request, CancellationToken cancellationToken)
    {
        var detail = await RouteRows.DetailAsync(db, caller, request.Code, cancellationToken);
        var company = await PdfCompany.LoadAsync(db, "Feuille de route", cancellationToken);
        var toSender = detail.Kind is "pickup" or "return";
        var sheet = new RouteSheet(company, detail.Code, detail.Kind, detail.CourierName, detail.CityName, detail.ExitDate,
            detail.Parcels.Select(p => new RouteSheetParcel(p.Code, p.RecipientName, p.RecipientPhone, p.DeliveryAddress, p.DestinationDistrictName, p.SenderName, p.SenderPhone, p.PickupAddress,
                p.OriginCityName, p.CodToCollect, p.Quantity)).ToList(),
            toSender ? detail.ConfirmedCount : detail.Parcels.Count(p => p.StatusId == CourierPay.Delivered), detail.RemovedCount);
        return ParcelLabels.Pdf(pdf.Render(sheet), $"{detail.Code}.pdf");
    }
}

/// <summary>`GET /routes/:code/labels` (`labelQuerySchema`) : les étiquettes des colis du circuit.</summary>
public sealed record GetRouteLabelsQuery(string Code, string? Format) : IRequest<FileContent>;

public sealed class GetRouteLabelsValidator : ZodValidator<GetRouteLabelsQuery>
{
    public GetRouteLabelsValidator()
    {
        RuleFor(x => x.Format).OneOf(ParcelLabels.Formats);
    }
}

internal sealed class GetRouteLabelsHandler(IAppDbContext db, ICurrentUser caller, IPdfService pdf, IFileStorage storage) : IRequestHandler<GetRouteLabelsQuery, FileContent>
{
    public async Task<FileContent> Handle(GetRouteLabelsQuery request, CancellationToken cancellationToken)
    {
        // Comme Speedy : le livreur n'atteint que ses circuits, sans la portée d'agence de la fiche.
        var route = await db.Routes.AsNoTracking().Where(r => r.Code == request.Code && (caller.CourierId == null || r.CourierId == caller.CourierId))
                        .Select(r => new { r.Id, r.Code }).FirstOrDefaultAsync(cancellationToken)
                    ?? throw BusinessException.NotFound($"Circuit introuvable : {request.Code}");
        var parcels = await ParcelLabels.LoadAsync(ParcelFilters.Scoped(db.Parcels.AsNoTracking(), caller).Where(p => p.RouteLinks.Any(l => l.RouteId == route.Id)).OrderBy(p => p.Code),
            cancellationToken);
        var sheet = await ParcelLabels.SheetAsync(db, storage, parcels, request.Format ?? LabelSheet.Format10X10, cancellationToken);
        return ParcelLabels.Pdf(pdf.Render(sheet), $"{route.Code}-etiquettes.pdf");
    }
}

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
