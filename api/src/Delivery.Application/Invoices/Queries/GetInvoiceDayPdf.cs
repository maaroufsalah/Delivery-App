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

/// <summary>`invoiceDayPdfQuerySchema` : les factures d'un jour (ou les brouillons) dans un seul PDF, 200 au plus.</summary>
public sealed record GetInvoiceDayPdfQuery(InvoiceListFilter Filter, string? Day) : IRequest<FileContent>;

internal sealed class GetInvoiceDayPdfHandler(IAppDbContext db, ICurrentUser caller, IPdfService pdf, IFileStorage storage) : IRequestHandler<GetInvoiceDayPdfQuery, FileContent>
{
    public async Task<FileContent> Handle(GetInvoiceDayPdfQuery request, CancellationToken cancellationToken)
    {
        var day = request.Day?.Trim();
        if (day is null || (day != InvoiceRows.DraftDay && !System.Text.RegularExpressions.Regex.IsMatch(day, @"^\d{4}-\d{2}-\d{2}$")))
        {
            throw BusinessException.Validation("Saisie invalide.", new { issues = new[] { new { path = "day", message = "Invalid input" } } });
        }
        var where = InvoiceLists.Where(db, caller, request.Filter with { IssuedDay = null });
        var draft = day == InvoiceRows.DraftDay;
        if (draft) where = where.Where(i => i.IssuedAt == null);
        else
        {
            var (start, end) = ReturnRows.DayRange(day, day);
            where = where.Where(i => i.IssuedAt >= start && i.IssuedAt < end);
        }
        var codes = await where.OrderBy(i => i.IssuedAt).ThenBy(i => i.CreatedAt).ThenBy(i => i.Code).Select(i => i.Code).Take(InvoiceRows.DayPdfMax + 1).ToListAsync(cancellationToken);
        if (codes.Count == 0) throw BusinessException.NotFound("Aucune facture pour ce jour.");
        if (codes.Count > InvoiceRows.DayPdfMax)
        {
            throw BusinessException.Validation($"Plus de {InvoiceRows.DayPdfMax} factures ce jour-là : resserrez la liste par un filtre.", new { reason = "too-many" });
        }
        var invoices = new List<InvoiceDetailDto>();
        foreach (var code in codes) invoices.Add(await CourierMasks.ForReaderAsync(db, caller, await InvoiceRows.DetailAsync(db, caller, code, cancellationToken), cancellationToken));
        var (company, proLegal, proTax, logo, footer) = await InvoicePdfs.LetterheadAsync(db, storage, invoices.Any(i => i.ClientIsPro), cancellationToken);
        var title = draft ? "Factures-brouillons" : $"Factures-{day}";
        var sheet = new InvoiceSheet(company, proLegal, proTax, logo, footer, title, invoices.Select(InvoicePdfs.Page).ToList());
        return new FileContent(pdf.Render(sheet), FileContent.Pdf, $"{title}.pdf", CacheControl: "private, no-store");
    }
}
