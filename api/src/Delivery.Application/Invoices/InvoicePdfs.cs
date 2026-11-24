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
using Delivery.Application.Finance;
using Delivery.Application.Notifications;
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
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Invoices;

/// <summary>Les données PDF d'une facture et l'identité qui l'imprime (Pro : raison sociale et ICE Pro, logo).</summary>
internal static class InvoicePdfs
{
    public static async Task<(PdfCompany Company, string? ProLegalName, string? ProTaxId, byte[]? Logo, string? Footer)> LetterheadAsync(IAppDbContext db, IFileStorage storage, bool withLogo,
        CancellationToken cancellationToken)
    {
        var company = await PdfCompany.LoadAsync(db, "Facture", cancellationToken);
        var identity = await db.Companies.AsNoTracking().OrderBy(c => c.CreatedAt).Select(c => new { c.Id, c.ProLegalName, c.ProTaxId, c.LogoUrl }).FirstOrDefaultAsync(cancellationToken);
        byte[]? logo = null;
        if (withLogo && identity is not null && Settings.Logos.KeyOf(identity.Id, identity.LogoUrl) is { } key && !key.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                logo = await storage.GetAsync(key, cancellationToken);
            }
            catch (BusinessException)
            {
                logo = null;
            }
        }
        var footer = await db.SettingAsync(SettingKeys.InvoiceFooter, cancellationToken) is { ValueKind: JsonValueKind.String } f ? f.GetString() : null;
        return (company, identity?.ProLegalName, identity?.ProTaxId, logo, footer);
    }

    public static InvoiceSheetPage Page(InvoiceDetailDto i) => new(i.Code, i.Status, i.ClientName, i.ClientIsPro, i.AgencyName, i.BillingType, i.PeriodStart, i.PeriodEnd, i.IssuedAt, i.DueAt,
        i.ParcelCount, i.TotalCod, i.TotalDeliveryFee, i.TotalFailureFee, i.TotalAdjustment, i.TotalVat, i.VatRate, i.ProVatRate, i.TotalDue, i.TotalPaid, i.Balance, i.Note,
        i.Lines.Select(l => new InvoiceSheetLine(l.ParcelCode, l.ExternalCode, l.RecipientName, l.RecipientPhone, l.DestinationCityName, l.DeliveredAt, l.StatusId, l.CodAmount,
            l.DeliveryFee + l.FailureFee, l.NetDue, l.ChequeRejected, l.ChequeNumber)).ToList(),
        i.Adjustments.Select(a => new InvoiceSheetAdjustment(a.Reason, a.Reference, a.Amount)).ToList(),
        i.Payments.Select(p => new InvoiceSheetPayment(p.PaidAt, p.BankAccountLabel, p.Reference, p.Amount)).ToList());
}
