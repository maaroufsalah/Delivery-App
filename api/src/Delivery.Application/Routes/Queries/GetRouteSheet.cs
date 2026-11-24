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
