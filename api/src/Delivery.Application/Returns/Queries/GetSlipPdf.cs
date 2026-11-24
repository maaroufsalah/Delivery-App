using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.CourierApp;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Application.Routes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using Delivery.Domain.Pricing;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Returns;

/// <summary>`GET /return-slips/:code/pdf` : masqué pour un vendeur.</summary>
public sealed record GetSlipPdfQuery(string Code) : IRequest<FileContent>;

internal sealed class GetSlipPdfHandler(IAppDbContext db, ICurrentUser caller, IPdfService pdf) : IRequestHandler<GetSlipPdfQuery, FileContent>
{
    public async Task<FileContent> Handle(GetSlipPdfQuery request, CancellationToken cancellationToken)
    {
        var s = await CourierMasks.ForReaderAsync(db, caller, await Slips.DetailAsync(db, caller, request.Code, cancellationToken), cancellationToken);
        var company = await PdfCompany.LoadAsync(db, "Bon de retour", cancellationToken);
        var sheet = new ReturnSlipSheet(company, s.Code, s.Status, s.AgencyName, s.ClientName, s.DeliveredAt, s.ReceivedByName, s.ParcelCount, s.TotalFees,
            s.Items.Select(i => new ReturnSlipSheetLine(i.ParcelCode, i.RecipientName, i.Reason, i.Quantity, i.FeeApplied)).ToList());
        return new FileContent(pdf.Render(sheet), FileContent.Pdf, $"{s.Code}.pdf", CacheControl: "private, no-store");
    }
}
