using System.Globalization;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.Routes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Returns;

/// <summary>`GET /return-handovers/:code/pdf`.</summary>
public sealed record GetHandoverPdfQuery(string Code) : IRequest<FileContent>;

internal sealed class GetHandoverPdfHandler(IAppDbContext db, ICurrentUser caller, IPdfService pdf) : IRequestHandler<GetHandoverPdfQuery, FileContent>
{
    public async Task<FileContent> Handle(GetHandoverPdfQuery request, CancellationToken cancellationToken)
    {
        var h = await Handovers.DetailAsync(db, caller, request.Code, cancellationToken);
        var company = await PdfCompany.LoadAsync(db, "Bon de retour", cancellationToken);
        var sheet = new ReturnHandoverSheet(company, h.Code, h.Status, h.Origin, h.AgencyName, h.CreatedAt, h.CourierName, h.CreatedByName, h.ClientNames, h.ConfirmedAt, h.PickedUpAt,
            h.ConfirmedByName, h.ParcelCount, h.Items.Select(i => new ReturnHandoverSheetLine(i.ParcelCode, i.RecipientName, i.ClientName, i.Quantity, i.StatusId)).ToList());
        return new FileContent(pdf.Render(sheet), FileContent.Pdf, $"{h.Code}.pdf", CacheControl: "private, no-store");
    }
}
