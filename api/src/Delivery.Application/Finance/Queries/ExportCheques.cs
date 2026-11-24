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
using Delivery.Application.Returns;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Finance;

/// <summary>`GET /cheques/export` : classeur « Chèques ».</summary>
public sealed record ExportChequesQuery(ChequeListFilter Filter) : IRequest<FileContent>;

public sealed class ExportChequesValidator : ZodValidator<ExportChequesQuery>
{
    public ExportChequesValidator()
    {
        RuleFor(x => x.Filter.Bank).MaxLength(120).OverridePropertyName("bank");
    }
}

internal sealed class ExportChequesHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportChequesQuery, FileContent>
{
    private static readonly Dictionary<string, string> Labels = new()
    {
        ["recu-livreur"] = "Reçu par le livreur", ["remis-admin"] = "Remis à l’agence", ["recu-admin"] = "Reçu par l’agence", ["transmis-vendeur"] = "Transmis au vendeur",
        ["recu-vendeur"] = "Réception confirmée par le vendeur", ["rejete"] = "Rejeté", ["annule"] = "Annulé",
    };

    private static readonly IReadOnlyList<ExportColumn<ChequeListItemDto>> Columns =
    [
        new("N° de chèque", r => r.Number, Text: true),
        new("Statut", r => Labels.GetValueOrDefault(r.Status, r.Status)),
        new("Banque", r => r.Bank ?? string.Empty),
        new("Montant", r => Exports.Amount(r.Amount)),
        new("Date du chèque", r => r.ChequeDate is null ? string.Empty : Exports.CalendarDateText(DateOnly.ParseExact(r.ChequeDate, "yyyy-MM-dd", CultureInfo.InvariantCulture))),
        new("Reçu le", r => Exports.DateTimeText(r.ReceivedAt)),
        new("Tireur", r => r.PayerName),
        new("Colis", r => r.ParcelCode, Text: true),
        new("Vendeur", r => r.ClientName),
        new("Livreur", r => r.CourierName),
        new("Facture", r => r.InvoiceCode ?? string.Empty, Text: true),
    ];

    public async Task<FileContent> Handle(ExportChequesQuery request, CancellationToken cancellationToken)
    {
        var where = ChequeLists.Filter(db, caller, request.Filter);
        Exports.AssertWithinLimit(await where.CountAsync(cancellationToken));
        return await Exports.ToXlsxAsync(db, excel, clock, caller, "Chèques", Columns, await Cheques.ListAsync(where.OrderByDescending(c => c.ReceivedAt), caller, cancellationToken), cancellationToken);
    }
}
