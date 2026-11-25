using System.Globalization;
using System.Text.Json;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Invoices;

/// <summary>`transferOrderCreateSchema` : des vendeurs OU des factures, jamais les deux.</summary>
public sealed record CreateTransferOrderCommand : IRequest<TransferOrderDetailDto>
{
    public IReadOnlyList<string>? ClientIds { get; init; }
    public IReadOnlyList<string>? InvoiceCodes { get; init; }
}

public sealed class CreateTransferOrderValidator : ZodValidator<CreateTransferOrderCommand>
{
    public CreateTransferOrderValidator()
    {
        RuleFor(x => x.ClientIds).Custom((ids, context) => Check(ids, "clientIds", context));
        RuleFor(x => x.InvoiceCodes).Custom((ids, context) => Check(ids, "invoiceCodes", context));
    }

    private static void Check(IReadOnlyList<string>? ids, string path, ValidationContext<CreateTransferOrderCommand> context)
    {
        if (ids is null) return;
        if (ids.Count > NoteRules.SelectionMax) context.AddFailure(ValidationPaths.Raw + path, "too-many");
        for (var i = 0; i < ids.Count; i++) if (string.IsNullOrEmpty(ids[i])) context.AddFailure(ValidationPaths.Raw + $"{path}.{i}", Zod.TooSmallString(1));
    }

    protected override void Refine(CreateTransferOrderCommand value, Action<string, string> issue)
    {
        if ((value.ClientIds?.Count ?? 0) > 0 == (value.InvoiceCodes?.Count ?? 0) > 0) issue(ValidationPaths.Raw + "clientIds", "required");
    }
}

internal sealed class CreateTransferOrderHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<CreateTransferOrderCommand, TransferOrderDetailDto>
{
    public async Task<TransferOrderDetailDto> Handle(CreateTransferOrderCommand input, CancellationToken cancellationToken)
    {
        var invoiceCodes = input.InvoiceCodes ?? [];
        var clientIds = input.ClientIds ?? [];
        var fromInvoices = invoiceCodes.Count > 0;
        if (fromInvoices)
        {
            var earlier = await db.TransferOrders.AsNoTracking().Where(o => o.InvoiceCodes.Any(c => invoiceCodes.Contains(c))).Select(o => new { o.Code, o.InvoiceCodes }).FirstOrDefaultAsync(cancellationToken);
            if (earlier is not null)
            {
                var twice = earlier.InvoiceCodes.Where(invoiceCodes.Contains).ToList();
                throw BusinessException.Conflict($"Déjà dans l’ordre de virement {earlier.Code} : {string.Join(", ", twice)}.",
                    new { reason = "invoice-already-transferred", transferOrder = earlier.Code, invoiceCodes = twice });
            }
        }
        var lines = fromInvoices
            ? await TransferOrders.ForInvoicesAsync(db, caller, invoiceCodes, cancellationToken)
            : (await InvoiceRows.PendingByClientAsync(db, caller, clientIds, null, true, cancellationToken)).Where(r => TransferRules.EntersSelection(r.Due))
                .Select(r => (Line: new TransferLineDto(r.ClientId, r.ClientName, r.ParcelCount, r.Due + r.Fees, r.Fees, r.Due), ParcelIds: r.ParcelIds, InvoiceCodes: (IReadOnlyList<string>)[])).ToList();
        if (lines.Count == 0)
        {
            var already = fromInvoices ? 0 : await ParcelFilters.Scoped(db.Parcels.AsNoTracking(), caller)
                .CountAsync(p => clientIds.Contains(p.ClientId) && p.FinancialStatus == FinancialStatus.REMIS_LIVREUR && !p.InvoiceLines.Any() && p.TransferOrders.Any(), cancellationToken);
            throw BusinessException.Conflict(already > 0 ? $"Rien à virer : {already} colis remis de cette sélection sont déjà dans un ordre de virement." : "Rien à virer pour cette sélection.",
                new { reason = "nothing-to-transfer", alreadyTransferred = already });
        }
        var now = DateTime.UtcNow;
        string code;
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            code = await DocumentCodes.ReserveAsync(db, TransferOrders.Prefix, now, cancellationToken);
            var order = new TransferOrder
            {
                Code = code, Source = fromInvoices ? TransferOrderSource.INVOICES : TransferOrderSource.SELECTION, AgencyId = caller.IsSuperAdmin() ? null : caller.AgencyId,
                Lines = JsonSerializer.Serialize(lines.Select(l => l.Line).ToList(), JsonSerializerOptions.Web), InvoiceCodes = fromInvoices ? lines.SelectMany(l => l.InvoiceCodes).Distinct().ToList() : [],
                TotalCollected = Money.FromCents(lines.Sum(l => l.Line.Collected)), TotalFees = Money.FromCents(lines.Sum(l => l.Line.Fees)), TotalDue = Money.FromCents(lines.Sum(l => l.Line.Due)),
                CreatedById = caller.UserId, CreatedAt = now,
            };
            db.TransferOrders.Add(order);
            foreach (var parcelId in lines.SelectMany(l => l.ParcelIds).Distinct()) db.TransferOrderParcels.Add(new TransferOrderParcel { TransferOrderId = order.Id, ParcelId = parcelId });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw BusinessException.Conflict("Un des colis de cette sélection vient d’être viré par un autre ordre de virement : rechargez avant de recommencer.", new { reason = "parcel-already-transferred" });
        }
        return await TransferOrders.DetailAsync(db, caller, code, cancellationToken);
    }
}
