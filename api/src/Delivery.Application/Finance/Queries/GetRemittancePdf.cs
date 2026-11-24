using System.Text.Json;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.Returns;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Finance;

/// <summary>`GET /remittances/:code/pdf` : le reçu de remise.</summary>
public sealed record GetRemittancePdfQuery(string Code) : IRequest<FileContent>;

internal sealed class GetRemittancePdfHandler(IAppDbContext db, ICurrentUser caller, IPdfService pdf) : IRequestHandler<GetRemittancePdfQuery, FileContent>
{
    public async Task<FileContent> Handle(GetRemittancePdfQuery request, CancellationToken cancellationToken)
    {
        var r = await Remittances.DetailAsync(db, caller, request.Code, cancellationToken);
        var company = await PdfCompany.LoadAsync(db, "Reçu de remise", cancellationToken);
        var sheet = new RemittanceSheet(company, r.Code, r.Status, r.AgencyName, r.CourierName, r.CityName, r.CreatedAt, r.SettlementMode, r.ParcelCount, r.ExpectedCod, r.TotalCheque,
            r.ChequeCount, r.TotalCash, r.ExpectedCommission, r.ExpectedNet, r.DeclaredAmount, r.Variance, r.VarianceReason, r.ConfirmedByName,
            r.Items.Select(i => new RemittanceSheetLine(i.ParcelCode, i.ClientName, i.RecipientName, i.Amount, i.Commission)).ToList(),
            r.Cheques.Select(c => new RemittanceSheetCheque(c.Number, c.Bank, c.ParcelCode, c.Amount)).ToList());
        return new FileContent(pdf.Render(sheet), FileContent.Pdf, $"{r.Code}.pdf", CacheControl: "private, no-store");
    }
}
