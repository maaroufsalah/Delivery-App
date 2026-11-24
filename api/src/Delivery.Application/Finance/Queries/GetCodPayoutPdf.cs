using System.Globalization;
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
using Delivery.Application.Parcels;
using Delivery.Application.PettyCash;
using Delivery.Application.Returns;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Finance;

/// <summary>`GET /cod-payouts/:code/pdf` : masqué pour un vendeur ; pied de page réglable.</summary>
public sealed record GetCodPayoutPdfQuery(string Code) : IRequest<FileContent>;

internal sealed class GetCodPayoutPdfHandler(IAppDbContext db, ICurrentUser caller, IPdfService pdf) : IRequestHandler<GetCodPayoutPdfQuery, FileContent>
{
    public async Task<FileContent> Handle(GetCodPayoutPdfQuery request, CancellationToken cancellationToken)
    {
        var p = await CourierMasks.ForReaderAsync(db, caller, await CodPayouts.DetailAsync(db, caller, request.Code, cancellationToken), cancellationToken);
        var company = await PdfCompany.LoadAsync(db, "Bon de versement", cancellationToken);
        var footer = await db.SettingAsync(SettingKeys.CodPayoutFooter, cancellationToken) is { ValueKind: JsonValueKind.String } f ? f.GetString() : null;
        var sheet = new CodPayoutSheet(company, footer, p.Code, p.Status, p.AgencyName, p.ClientName, p.Method, p.BankAccountLabel, p.PaidAt, p.Reference, p.ParcelCount, p.Amount, p.Note,
            p.PaidByName, p.Items.Select(i => new CodPayoutSheetLine(i.ParcelCode, i.RecipientName, i.DestinationCityName, i.Amount)).ToList(),
            p.Cheques.Select(c => new CodPayoutSheetCheque(c.Number, c.Bank, c.ParcelCode, c.Status, c.Amount, c.IncludedInPayout)).ToList());
        return new FileContent(pdf.Render(sheet), FileContent.Pdf, $"{p.Code}.pdf", CacheControl: "private, no-store");
    }
}
