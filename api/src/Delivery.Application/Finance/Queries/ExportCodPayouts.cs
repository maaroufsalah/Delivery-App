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

/// <summary>`GET /cod-payouts/export` : classeur « Bons de versement ».</summary>
public sealed record ExportCodPayoutsQuery(CodPayoutListFilter Filter) : IRequest<FileContent>;

internal sealed class ExportCodPayoutsHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportCodPayoutsQuery, FileContent>
{
    private static readonly IReadOnlyList<ExportColumn<CodPayoutListItemDto>> Columns =
    [
        new("Bon de versement", r => r.Code),
        new("Statut", r => DocumentLabels.Enum(r.Status)),
        new("E-commerçant", r => r.ClientName),
        new("Agence", r => r.AgencyName),
        new("Colis", r => r.ParcelCount),
        new("Montant versé", r => Exports.Amount(r.Amount)),
        new("Mode", r => DocumentLabels.Enum(r.Method)),
        new("Compte", r => r.BankAccountLabel ?? string.Empty),
        new("Référence", r => r.Reference ?? string.Empty, Text: true),
        new("Versé le", r => Exports.DateTimeText(r.PaidAt)),
        new("Versé par", r => r.PaidByName ?? string.Empty),
        new("Créé le", r => Exports.DateTimeText(r.CreatedAt)),
    ];

    public async Task<FileContent> Handle(ExportCodPayoutsQuery request, CancellationToken cancellationToken)
    {
        var where = CodPayoutLists.Filter(db, caller, request.Filter);
        Exports.AssertWithinLimit(await where.CountAsync(cancellationToken));
        var rows = (await CodPayouts.Project(where).ToListAsync(cancellationToken)).Select(CodPayouts.Wire).ToList();
        return await Exports.ToXlsxAsync(db, excel, clock, caller, "Bons de versement", Columns, rows, cancellationToken);
    }
}
