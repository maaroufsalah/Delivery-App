using System.Globalization;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Finance;
using Delivery.Application.Parcels;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using Delivery.Domain.Parcels;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Invoices;

/// <summary>`InvoicesService` : portée, projections, montants d'une ligne, règles de facturabilité.</summary>
public static class InvoiceRows
{
    public const string DraftDay = "draft";
    public const int DayPdfMax = 200;
    public static readonly string[] Statuses = ["draft", "issued", "paid", "carried-over"];

    /// <summary>`invoiceWhere` : administrateur tout, vendeur ses factures émises, livreur rien, agent son agence.</summary>
    public static IQueryable<Invoice> Scoped(IQueryable<Invoice> invoices, ICurrentUser caller)
    {
        if (caller.IsSuperAdmin()) return invoices;
        if (caller.ClientId is { } clientId) return invoices.Where(i => i.ClientId == clientId && i.Status != InvoiceStatus.DRAFT);
        if (caller.CourierId is not null) return invoices.Where(i => i.Id == Scope.DenyAllId);
        if (caller.AgencyId is { } agencyId) return invoices.Where(i => i.AgencyId == agencyId);
        return invoices.Where(i => i.Id == Scope.DenyAllId);
    }

    /// <summary>`paymentWhere`.</summary>
    public static IQueryable<Payment> Payments(IQueryable<Payment> payments, ICurrentUser caller)
    {
        if (caller.IsSuperAdmin()) return payments;
        if (caller.ClientId is { } clientId) return payments.Where(p => p.ClientId == clientId);
        if (caller.CourierId is not null) return payments.Where(p => p.Id == Scope.DenyAllId);
        if (caller.AgencyId is { } agencyId) return payments.Where(p => p.AgencyId == agencyId);
        return payments.Where(p => p.Id == Scope.DenyAllId);
    }

    public static string WireBillingType(BillingType type) => type.ToString().ToLowerInvariant();

    public static IQueryable<InvoiceListItemDto> Project(IQueryable<Invoice> invoices) => invoices.Select(i => new InvoiceListItemDto
    {
        Id = i.Id, Code = i.Code, Status = i.Status == InvoiceStatus.DRAFT ? "draft" : i.Status == InvoiceStatus.ISSUED ? "issued" : i.Status == InvoiceStatus.CARRIED_OVER ? "carried-over" : "paid", ClientId = i.ClientId, ClientName = i.Client.Name,
        ClientIsPro = i.Client.IsPro, AgencyId = i.AgencyId, AgencyName = i.Agency.Name, ParcelCount = i.Lines.Count, TotalCod = (long)Math.Round(i.TotalCod * 100m),
        TotalDeliveryFee = (long)Math.Round(i.TotalDeliveryFee * 100m), TotalFailureFee = (long)Math.Round(i.TotalFailureFee * 100m), TotalAdjustment = (long)Math.Round(i.TotalAdjustment * 100m),
        TotalVat = (long)Math.Round(i.TotalVat * 100m), VatRate = i.VatRate, ProVatRate = i.ProVatRate, TotalDue = (long)Math.Round(i.TotalDue * 100m),
        BillingType = i.BillingType == Domain.Enums.BillingType.PROFESSIONAL ? "professional" : "normal", PeriodStart = i.PeriodStart, PeriodEnd = i.PeriodEnd,
        TotalPaid = (long)Math.Round(i.Payments.Sum(p => p.Amount) * 100m), CarriedIntoCode = i.CarriedInto != null ? i.CarriedInto.Code : null, IssuedAt = i.IssuedAt, DueAt = i.DueAt, PaidAt = i.PaidAt, CreatedAt = i.CreatedAt,
    });

    public static IQueryable<PaymentListItemDto> ProjectPayments(IQueryable<Payment> payments) => payments.Select(p => new PaymentListItemDto(p.Id, p.InvoiceId,
        p.Invoice != null ? p.Invoice.Code : null, p.ClientId, p.Client.Name, p.AgencyId, p.Agency.Name, (long)Math.Round(p.Amount * 100m),
        p.Method == PaymentMethod.CASH ? "cash" : p.Method == PaymentMethod.TRANSFER ? "transfer" : "check", p.BankAccountId, p.BankAccount != null ? p.BankAccount.Label : null, p.Reference,
        p.Note, p.ChequeNumber, p.ChequeBank, null, p.ProofName, p.PaidAt, p.CreatedAt));

    /// <summary>La date de chèque (colonne `date`) n'est pas projetable en `DateTime` côté base : relue après coup.</summary>
    public static async Task<List<PaymentListItemDto>> PaymentsAsync(IAppDbContext db, IQueryable<Payment> payments, CancellationToken cancellationToken)
    {
        var rows = await ProjectPayments(payments).ToListAsync(cancellationToken);
        if (rows.Count == 0) return rows;
        var ids = rows.Select(r => r.Id).ToList();
        var dates = await db.Payments.AsNoTracking().Where(p => ids.Contains(p.Id) && p.ChequeDate != null).Select(p => new { p.Id, p.ChequeDate }).ToDictionaryAsync(p => p.Id, p => p.ChequeDate, cancellationToken);
        return rows.Select(r => dates.TryGetValue(r.Id, out var date) && date is { } d ? r with { ChequeDate = DateTime.SpecifyKind(d.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc) } : r).ToList();
    }

    public static long Outstanding(long totalDue, long totalPaid)
    {
        var settled = Math.Min(totalPaid, Math.Abs(totalDue));
        return totalDue >= 0 ? totalDue - settled : totalDue + settled;
    }

    public static async Task<InvoiceDetailDto> DetailAsync(IAppDbContext db, ICurrentUser caller, string code, CancellationToken cancellationToken)
    {
        var scoped = Scoped(db.Invoices.AsNoTracking(), caller).Where(i => i.Code == code);
        var item = await Project(scoped).FirstOrDefaultAsync(cancellationToken) ?? throw BusinessException.NotFound($"Facture introuvable : {code}");
        var extra = await scoped.Select(i => new { i.Note, IssuedBy = i.IssuedBy != null ? i.IssuedBy.FirstName + " " + i.IssuedBy.LastName : null }).FirstAsync(cancellationToken);
        var lines = await db.InvoiceLines.AsNoTracking().Where(l => l.InvoiceId == item.Id).OrderBy(l => l.ParcelCode)
            .Select(l => new
            {
                l.Id, l.ParcelId, l.ParcelCode, l.StatusId, l.CodAmount, l.DeliveryFee, l.FailureFee, l.VatAmount, l.ChequePaid, l.NetDue, l.Parcel.DeletedAt, l.Parcel.RecipientName,
                l.Parcel.ExternalCode, l.Parcel.RecipientPhone, City = l.Parcel.DestinationCity.Name, ChequeNumber = l.Parcel.Cheque != null ? l.Parcel.Cheque.Number : null,
                ChequeStatus = l.Parcel.Cheque != null ? l.Parcel.Cheque.Status : (ChequeStatus?)null,
                DeliveredAt = l.Parcel.Events.Where(e => e.StatusId == Billing.DeliveredStatusId).OrderByDescending(e => e.CreatedAt).Select(e => (DateTime?)e.CreatedAt).FirstOrDefault(),
            }).ToListAsync(cancellationToken);
        var adjustments = await db.InvoiceAdjustments.AsNoTracking().Where(a => a.InvoiceId == item.Id).OrderBy(a => a.CreatedAt)
            .Select(a => new { a.Id, a.Kind, a.Amount, a.VatAmount, a.Reason, a.Reference, a.CreatedByName, a.CreatedAt }).ToListAsync(cancellationToken);
        var payments = await PaymentsAsync(db, db.Payments.AsNoTracking().Where(p => p.InvoiceId == item.Id).OrderByDescending(p => p.PaidAt), cancellationToken);
        static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        var detail = new InvoiceDetailDto
        {
            Id = item.Id, Code = item.Code, Status = item.Status, ClientId = item.ClientId, ClientName = item.ClientName, ClientIsPro = item.ClientIsPro, AgencyId = item.AgencyId,
            AgencyName = item.AgencyName, ParcelCount = item.ParcelCount, TotalCod = item.TotalCod, TotalDeliveryFee = item.TotalDeliveryFee, TotalFailureFee = item.TotalFailureFee,
            TotalAdjustment = item.TotalAdjustment, TotalVat = item.TotalVat, VatRate = item.VatRate, ProVatRate = item.ProVatRate, TotalDue = item.TotalDue, BillingType = item.BillingType,
            PeriodStart = item.PeriodStart, PeriodEnd = item.PeriodEnd, TotalPaid = item.TotalPaid, CarriedIntoCode = item.CarriedIntoCode, IssuedAt = item.IssuedAt, DueAt = item.DueAt, PaidAt = item.PaidAt, CreatedAt = item.CreatedAt,
            Note = extra.Note, IssuedByName = extra.IssuedBy,
            Lines = lines.Select(l => l.DeletedAt is not null
                ? new InvoiceLineDto(l.Id, l.ParcelId, l.ParcelCode, null, null, null, null, null, l.StatusId, Money.ToCents(l.CodAmount), Money.ToCents(l.DeliveryFee), Money.ToCents(l.FailureFee),
                    Money.ToCents(l.VatAmount), Money.ToCents(l.ChequePaid), l.ChequeNumber, l.ChequeStatus == ChequeStatus.REJETE, Money.ToCents(l.NetDue))
                : new InvoiceLineDto(l.Id, l.ParcelId, l.ParcelCode, Text(l.RecipientName), Text(l.ExternalCode), Text(l.RecipientPhone), Text(l.City), l.DeliveredAt, l.StatusId,
                    Money.ToCents(l.CodAmount), Money.ToCents(l.DeliveryFee), Money.ToCents(l.FailureFee), Money.ToCents(l.VatAmount), Money.ToCents(l.ChequePaid), l.ChequeNumber,
                    l.ChequeStatus == ChequeStatus.REJETE, Money.ToCents(l.NetDue))).ToList(),
            Adjustments = adjustments.Select(a => new InvoiceAdjustmentDto(a.Id, Money.ToWire(a.Kind), Money.ToCents(a.Amount), Money.ToCents(a.VatAmount), a.Reason, a.Reference, a.CreatedByName,
                a.CreatedAt)).ToList(),
            Payments = payments,
            Issuable = item.Status == "draft" && (lines.Count > 0 || adjustments.Count > 0),
            Adjustable = item.Status == "draft",
            // Reportée : plus rien à régler sur elle, le net vit sur la facture qui l'absorbe.
            Balance = item.Status == "carried-over" ? 0 : Outstanding(item.TotalDue, item.TotalPaid),
        };
        return detail;
    }

    /// <summary>`assertInvoiceKind` : la facture d'un Client Pro se fait dans « Factures clients pro », et inversement.</summary>
    public static void AssertKind(string clientName, bool isPro, string? kind)
    {
        if (kind is null || isPro == (kind == "pro")) return;
        throw BusinessException.Validation(kind == "pro"
                ? $"{clientName} n’est pas un Client Pro : sa facture se fait dans « Factures clients »."
                : $"{clientName} est un Client Pro : sa facture se fait dans « Factures clients pro ».",
            new { reason = "invoice-client-kind", clientName, expected = kind });
    }

    /// <summary>`startOfMonthUtc` : la frontière du mois, en UTC comme chez Speedy.</summary>
    public static DateTime StartOfMonthUtc(DateTime now) => new(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>`billableNowWhere` : livré et cash remis, ou échec / retour d'un mois clos.</summary>
    public static IQueryable<Parcel> BillableNow(IQueryable<Parcel> parcels, DateTime now)
    {
        var monthStart = StartOfMonthUtc(now);
        int[] failed = [StatusCatalog.Returned, StatusCatalog.Refused];
        var returnFlow = StatusCatalog.ReturnFlowStatusIds;
        return parcels.Where(p => (p.StatusId == Billing.DeliveredStatusId && p.ReimbursedByCourier) || (failed.Contains(p.StatusId) && p.StatusAt < monthStart)
                                  || (returnFlow.Contains(p.StatusId) && p.IsFailed && p.StatusAt < monthStart));
    }

    /// <summary>`billabilityOf`.</summary>
    public static string Billability(int statusId, bool reimbursed, DateTime statusAt, long deliveryFee, long? failureFee, DateTime now)
    {
        var billable = statusId == Billing.DeliveredStatusId ? reimbursed : statusAt < StartOfMonthUtc(now);
        if (!billable) return "not-billable";
        var charged = statusId == Billing.DeliveredStatusId ? deliveryFee : failureFee ?? 0;
        return charged == 0 ? "billable-free" : "billable";
    }

    /// <summary>`isBillableParcel`.</summary>
    public static bool IsBillableParcel(int statusId, bool isFailed) => StatusCatalog.IsBillable(statusId) || (isFailed && statusId is >= 20 and <= 24);

    public sealed record Amounts(long Cod, long DeliveryFee, long FailureFee, long Vat, long ChequePaid, long NetDue);

    /// <summary>`lineAmounts` : encaissé, frais de livraison ou d'échec, TVA, part chèque et net dû d'une ligne.</summary>
    public static Amounts LineAmounts(BillableParcel parcel, int vatRate, BillingType billingType, long chequePaid)
    {
        var delivered = parcel.StatusId == Billing.DeliveredStatusId;
        var fee = Billing.FeeCharged(parcel);
        return new Amounts(Billing.CodCollected(parcel), delivered ? fee : 0, delivered ? 0 : fee, Billing.VatOn(fee, vatRate), chequePaid,
            Billing.NetDueFor(WireBillingType(billingType), parcel, Billing.DeliveredStatusId, vatRate, chequePaid));
    }

    public static InvoiceLine LineOf(string invoiceId, string parcelId, string parcelCode, int statusId, Amounts amounts) => new()
    {
        InvoiceId = invoiceId, ParcelId = parcelId, ParcelCode = parcelCode, StatusId = statusId, CodAmount = Money.FromCents(amounts.Cod), DeliveryFee = Money.FromCents(amounts.DeliveryFee),
        FailureFee = Money.FromCents(amounts.FailureFee), VatAmount = Money.FromCents(amounts.Vat), ChequePaid = Money.FromCents(amounts.ChequePaid), NetDue = Money.FromCents(amounts.NetDue),
    };

    public static void Apply(InvoiceLine line, int statusId, Amounts amounts)
    {
        line.StatusId = statusId;
        line.CodAmount = Money.FromCents(amounts.Cod);
        line.DeliveryFee = Money.FromCents(amounts.DeliveryFee);
        line.FailureFee = Money.FromCents(amounts.FailureFee);
        line.VatAmount = Money.FromCents(amounts.Vat);
        line.ChequePaid = Money.FromCents(amounts.ChequePaid);
        line.NetDue = Money.FromCents(amounts.NetDue);
    }

    /// <summary>Ce qu'il faut d'un colis pour le facturer : montants, frais, chèque et remise en caisse.</summary>
    public sealed record ParcelMoney(string Id, string Code, string ClientId, string AgencyId, int StatusId, bool IsFailed, decimal CodAmount, decimal DeliveryFee, decimal? FailureFee,
        bool FeeIncluded, bool AlreadyPaid, decimal? ChequeAmount, ChequeStatus? ChequeStatus, List<ParcelCheques.Remitted> Remitted, DateTime StatusAt)
    {
        public BillableParcel Billable => new(Money.ToCents(CodAmount), Money.ToCents(DeliveryFee), Money.ToCentsOrNull(FailureFee), 0, FeeIncluded, AlreadyPaid, StatusId);

        public long ChequePaid => ParcelCheques.PaidOn(ChequeAmount, ChequeStatus, Remitted);
    }

    public static IQueryable<ParcelMoney> MoneyOf(IQueryable<Parcel> parcels) => parcels.Select(p => new ParcelMoney(p.Id, p.Code, p.ClientId, p.AgencyId, p.StatusId, p.IsFailed, p.CodAmount,
        p.DeliveryFee, p.FailureFee, p.FeeIncluded, p.AlreadyPaid, p.Cheque == null ? null : p.Cheque.Amount, p.Cheque == null ? null : p.Cheque.Status,
        p.Remittances.Where(r => r.Remittance.Status != CashRemittanceStatus.CANCELLED).Select(r => new ParcelCheques.Remitted(r.Cheque, r.ConfirmedParcelId)).ToList(), p.StatusAt));

    /// <summary>`settle` : la facture soldée, ses colis « remboursés ».</summary>
    public static async Task SettleAsync(IAppDbContext db, string invoiceId, DateTime at, string? byId, CancellationToken cancellationToken)
    {
        var parcelIds = await db.InvoiceLines.AsNoTracking().Where(l => l.InvoiceId == invoiceId).Select(l => l.ParcelId).ToListAsync(cancellationToken);
        await db.Invoices.Where(i => i.Id == invoiceId).ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, InvoiceStatus.PAID).SetProperty(i => i.PaidAt, at).SetProperty(i => i.PaidById, byId),
            cancellationToken);
        await db.Parcels.Where(p => parcelIds.Contains(p.Id)).ExecuteUpdateAsync(s => s.SetProperty(p => p.BillingState, BillingState.REIMBURSED)
            .SetProperty(p => p.FinancialStatus, FinancialStatus.REMBOURSE), cancellationToken);
    }

    public sealed record Draft(string Id, string ClientId, string AgencyId, BillingType BillingType, bool IsPro);

    public static async Task<Draft> RequireDraftAsync(IAppDbContext db, ICurrentUser caller, string code, CancellationToken cancellationToken)
    {
        var invoice = await Scoped(db.Invoices.AsNoTracking(), caller).Where(i => i.Code == code)
                          .Select(i => new { i.Id, i.Status, i.ClientId, i.AgencyId, i.BillingType, i.Client.IsPro }).FirstOrDefaultAsync(cancellationToken)
                      ?? throw BusinessException.NotFound($"Facture introuvable : {code}");
        if (invoice.Status != InvoiceStatus.DRAFT) throw BusinessException.Conflict("Cette facture est émise : elle ne se modifie plus.", new { reason = "invoice-issued" });
        return new Draft(invoice.Id, invoice.ClientId, invoice.AgencyId, invoice.BillingType, invoice.IsPro);
    }

    public sealed record Pending(string ClientId, string ClientName, string AgencyId, string AgencyName, int ParcelCount, long Fees, long Due, List<string> ParcelIds);

    /// <summary>
    /// `pendingByClient` : les colis remis par le livreur et jamais facturés, regroupés par vendeur ; la TVA d'un
    /// Client Pro se calcule sur le total des frais du groupe.
    /// </summary>
    public static async Task<List<Pending>> PendingByClientAsync(IAppDbContext db, ICurrentUser caller, IReadOnlyList<string>? clientIds, string? kind, bool forTransfer,
        CancellationToken cancellationToken)
    {
        var vatRate = await InvoiceDrafts.VatRateAsync(db, cancellationToken);
        var where = ParcelFilters.Scoped(db.Parcels.AsNoTracking(), caller).Where(p => p.FinancialStatus == FinancialStatus.REMIS_LIVREUR && !p.InvoiceLines.Any());
        if (clientIds is not null) where = where.Where(p => clientIds.Contains(p.ClientId));
        if (kind is not null) { var pro = kind == "pro"; where = where.Where(p => p.Client.IsPro == pro); }
        if (forTransfer) where = where.Where(p => !p.TransferOrders.Any());
        var rows = await where.OrderBy(p => p.StatusAt)
            .Select(p => new
            {
                Money = new ParcelMoney(p.Id, p.Code, p.ClientId, p.AgencyId, p.StatusId, p.IsFailed, p.CodAmount, p.DeliveryFee, p.FailureFee, p.FeeIncluded, p.AlreadyPaid,
                    p.Cheque == null ? null : p.Cheque.Amount, p.Cheque == null ? null : p.Cheque.Status,
                    p.Remittances.Where(r => r.Remittance.Status != CashRemittanceStatus.CANCELLED).Select(r => new ParcelCheques.Remitted(r.Cheque, r.ConfirmedParcelId)).ToList(), p.StatusAt),
                ClientName = p.Client.Name, p.Client.BillingType, ClientAgencyId = p.Client.AgencyId, p.Client.IsPro, ClientVatRate = p.Client.VatRate, AgencyName = p.Agency.Name,
                PaidOut = p.CodPayouts.Any(l => l.PaidOutParcelId != null),
            }).ToListAsync(cancellationToken);
        var proDefault = rows.Any(r => r.IsPro) ? await InvoiceDrafts.ProVatDefaultAsync(db, cancellationToken) : Billing.DefaultProVatRate;
        var proRates = new Dictionary<string, int>();
        var groups = new List<Pending>();
        foreach (var row in rows)
        {
            if (row.BillingType == BillingType.NORMAL && row.PaidOut) continue;
            var index = groups.FindIndex(g => g.ClientId == row.Money.ClientId);
            var current = index < 0 ? new Pending(row.Money.ClientId, row.ClientName, row.ClientAgencyId ?? row.Money.AgencyId, row.AgencyName, 0, 0, 0, []) : groups[index];
            var proRate = Billing.ProVatRateFor(row.IsPro, row.ClientVatRate, proDefault);
            if (proRate is { } rate) proRates[row.Money.ClientId] = rate;
            var amounts = LineAmounts(row.Money.Billable, proRate is null ? vatRate : 0, row.BillingType, row.Money.ChequePaid);
            current.ParcelIds.Add(row.Money.Id);
            var next = current with { ParcelCount = current.ParcelCount + 1, Due = current.Due + amounts.NetDue, Fees = current.Fees + amounts.DeliveryFee + amounts.FailureFee + amounts.Vat };
            if (index < 0) groups.Add(next);
            else groups[index] = next;
        }
        for (var i = 0; i < groups.Count; i++)
        {
            if (!proRates.TryGetValue(groups[i].ClientId, out var rate)) continue;
            var vat = Billing.VatOnTotal(groups[i].Fees, rate);
            groups[i] = groups[i] with { Fees = groups[i].Fees + vat, Due = groups[i].Due - vat };
        }
        // Les reports en attente : la prochaine facture du vendeur les absorbera. Un OV « sélection » tiré avant elle, ou la liste
        // à cocher, doivent déjà les retrancher (comptés comme des frais, comme la ligne de report : l'encaissé reste le COD hors chèques).
        if (groups.Count > 0)
        {
            var groupIds = groups.Select(g => g.ClientId).ToList();
            var carried = await db.Invoices.AsNoTracking().Where(i => groupIds.Contains(i.ClientId) && i.Status == InvoiceStatus.CARRIED_OVER && i.CarriedIntoId == null)
                .Select(i => new { i.ClientId, i.TotalDue }).ToListAsync(cancellationToken);
            foreach (var from in carried)
            {
                var index = groups.FindIndex(g => g.ClientId == from.ClientId);
                var owed = CarryOver.LineAmount(Money.ToCents(from.TotalDue));
                groups[index] = groups[index] with { Fees = groups[index].Fees + owed, Due = groups[index].Due - owed };
            }
        }
        return groups.OrderBy(g => g.ClientName, StringComparer.Create(CultureInfo.InvariantCulture, false)).ToList();
    }

    public static async Task<string> AuthorNameAsync(IAppDbContext db, string? userId, CancellationToken cancellationToken) => await Remittances.AuthorNameAsync(db, userId, cancellationToken);
}
