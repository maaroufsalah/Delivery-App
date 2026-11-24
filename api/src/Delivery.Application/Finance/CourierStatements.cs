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

namespace Delivery.Application.Finance;

/// <summary>`CourierStatementsService` : portée, calcul des courses et projections.</summary>
public static class CourierStatements
{
    public const string DefaultPrefix = "PAI";
    private const int ReturnedStatusId = 5;
    private const int RefusedStatusId = 6;

    public sealed record ComputedLine(string ParcelId, CourierStatementLineKind Kind, string ParcelCode, string CityName, int StatusId, long Amount, bool Deducted, DateTime WorkedAt);

    public sealed record Totals(long Base, long CommissionsTotal, long CommissionsDeducted, long Variable, long Due);

    /// <summary>`statementWhere` : administrateur tout, livreur les siens, vendeur rien, agent son agence.</summary>
    public static IQueryable<CourierStatement> Scoped(IQueryable<CourierStatement> statements, ICurrentUser caller)
    {
        if (caller.IsSuperAdmin()) return statements;
        if (caller.CourierId is { } courierId) return statements.Where(s => s.CourierId == courierId);
        if (caller.ClientId is not null) return statements.Where(s => s.Id == Scope.DenyAllId);
        if (caller.AgencyId is { } agencyId) return statements.Where(s => s.AgencyId == agencyId);
        return statements.Where(s => s.Id == Scope.DenyAllId);
    }

    public static IQueryable<CourierStatementListItemDto> Project(IQueryable<CourierStatement> statements) => statements.Select(s => new CourierStatementListItemDto
    {
        Id = s.Id, Code = s.Code, Status = s.Status == CourierStatementStatus.DRAFT ? "draft" : s.Status == CourierStatementStatus.VALIDATED ? "validated" : "paid", CourierId = s.CourierId,
        CourierName = s.Courier.FirstName + " " + s.Courier.LastName, AgencyId = s.AgencyId, AgencyName = s.Agency.Name, PeriodStart = s.PeriodStart, PeriodEnd = s.PeriodEnd,
        PeriodKind = s.PeriodKind.ToString(), PayModel = s.PayModel.ToString(), SettlementMode = s.SettlementMode.ToString(), LineCount = s.Lines.Count,
        BaseAmount = (long)Math.Round(s.BaseAmount * 100m), CodCollected = (long)Math.Round(s.CodCollected * 100m), CommissionsTotal = (long)Math.Round(s.CommissionsTotal * 100m),
        CommissionsDeducted = (long)Math.Round(s.CommissionsDeducted * 100m), VariableAmount = (long)Math.Round(s.VariableAmount * 100m), TotalDue = (long)Math.Round(s.TotalDue * 100m),
        ValidatedAt = s.ValidatedAt, ValidatedByName = s.ValidatedBy != null ? s.ValidatedBy.FirstName + " " + s.ValidatedBy.LastName : null, PaidAt = s.PaidAt, CreatedAt = s.CreatedAt,
    });

    /// <summary>Les énumérations projetées en base sous leur nom : remises au format du fil après lecture.</summary>
    public static T Wire<T>(T row) where T : CourierStatementListItemDto
    {
        row.PeriodKind = Money.ToWire(row.PeriodKind);
        row.PayModel = Money.ToWire(row.PayModel);
        row.SettlementMode = Money.ToWire(row.SettlementMode);
        return row;
    }

    public static async Task<CourierStatementDetailDto> DetailAsync(IAppDbContext db, ICurrentUser caller, string code, CancellationToken cancellationToken)
    {
        var scoped = Scoped(db.CourierStatements.AsNoTracking(), caller).Where(s => s.Code == code);
        var row = Wire(await Project(scoped).FirstOrDefaultAsync(cancellationToken) ?? throw BusinessException.NotFound($"Relevé introuvable : {code}"));
        var extra = await scoped.Select(s => new
        {
            s.Note, s.PaymentMethod, s.BankAccountId, BankAccountLabel = s.BankAccount != null ? s.BankAccount.Label : null, s.Reference, s.ChequeNumber, s.ChequeBank, s.ChequeDate, s.ProofName,
        }).FirstAsync(cancellationToken);
        var lines = await db.CourierStatementLines.AsNoTracking().Where(l => l.StatementId == row.Id).OrderBy(l => l.WorkedAt).ThenBy(l => l.ParcelCode)
            .Select(l => new { l.Id, l.ParcelId, l.Kind, l.ParcelCode, l.CityName, l.StatusId, l.Amount, l.Deducted, l.WorkedAt }).ToListAsync(cancellationToken);
        return new CourierStatementDetailDto
        {
            Id = row.Id, Code = row.Code, Status = row.Status, CourierId = row.CourierId, CourierName = row.CourierName, AgencyId = row.AgencyId, AgencyName = row.AgencyName,
            PeriodStart = row.PeriodStart, PeriodEnd = row.PeriodEnd, PeriodKind = row.PeriodKind, PayModel = row.PayModel, SettlementMode = row.SettlementMode, LineCount = row.LineCount,
            BaseAmount = row.BaseAmount, CodCollected = row.CodCollected, CommissionsTotal = row.CommissionsTotal, CommissionsDeducted = row.CommissionsDeducted,
            VariableAmount = row.VariableAmount, TotalDue = row.TotalDue, ValidatedAt = row.ValidatedAt, ValidatedByName = row.ValidatedByName, PaidAt = row.PaidAt, CreatedAt = row.CreatedAt,
            Note = extra.Note, Validatable = row.Status == "draft", PaymentMethod = Money.ToWire(extra.PaymentMethod), BankAccountId = extra.BankAccountId, BankAccountLabel = extra.BankAccountLabel,
            Reference = extra.Reference, ChequeNumber = extra.ChequeNumber, ChequeBank = extra.ChequeBank,
            ChequeDate = extra.ChequeDate is { } date ? DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc) : null, ProofName = extra.ProofName,
            Lines = lines.Select(l => new CourierStatementLineDto(l.Id, l.ParcelId, Money.ToWire(l.Kind), l.ParcelCode, l.CityName, l.StatusId, Money.ToCents(l.Amount), l.Deducted, l.WorkedAt)).ToList(),
        };
    }

    private static CourierStatementLineKind? KindOf(RouteKind routeKind, int? finalStatusId) => routeKind == RouteKind.PICKUP ? CourierStatementLineKind.PICKUP
        : finalStatusId == Billing.DeliveredStatusId ? CourierStatementLineKind.DELIVERY
        : finalStatusId is ReturnedStatusId or RefusedStatusId ? CourierStatementLineKind.RETURN
        : null;

    /// <summary>
    /// `computeLines` : chaque course confirmée sur la période et pas encore payée ; montant figé à la confirmation,
    /// sinon tarif négocié, sinon grille du livreur (ville du colis, puis ville du circuit), retour au forfait.
    /// </summary>
    public static async Task<List<ComputedLine>> ComputeLinesAsync(IAppDbContext db, string courierId, DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        var links = await db.RouteParcels.AsNoTracking().Where(l => l.Confirmed && l.ConfirmedAt >= start && l.ConfirmedAt <= end && l.Route.CourierId == courierId).OrderBy(l => l.ConfirmedAt)
            .Select(l => new
            {
                l.ParcelId, l.FinalStatusId, l.CourierCommission, l.CourierFee, l.ConfirmedAt, RouteKind = l.Route.Kind, RouteCityId = l.Route.CityId, l.Parcel.Code, l.Parcel.OriginCityId,
                l.Parcel.DestinationCityId, Origin = l.Parcel.OriginCity.Name, Destination = l.Parcel.DestinationCity.Name,
            }).ToListAsync(cancellationToken);
        var grid = await db.CourierCities.AsNoTracking().Where(c => c.CourierId == courierId).Select(c => new { c.CityId, c.PickupFee, c.DeliveryFee }).ToListAsync(cancellationToken);
        var returnFee = await db.SettingAsync(SettingKeys.CourierReturnFee, cancellationToken) is { ValueKind: JsonValueKind.Number } fee ? (long)fee.GetDouble() : 0;
        var pickupFee = grid.ToDictionary(g => g.CityId, g => Money.ToCents(g.PickupFee));
        var deliveryFee = grid.ToDictionary(g => g.CityId, g => Money.ToCents(g.DeliveryFee));
        var parcelIds = links.Select(l => l.ParcelId).Distinct().ToList();
        var settled = (await db.CourierStatementLines.AsNoTracking().Where(l => l.SettledParcelId != null && parcelIds.Contains(l.SettledParcelId)).Select(l => new { l.SettledParcelId, l.Kind })
            .ToListAsync(cancellationToken)).Select(l => $"{l.SettledParcelId}:{l.Kind}").ToHashSet();
        var deducted = (await db.CashRemittanceParcels.AsNoTracking().Where(l => parcelIds.Contains(l.ParcelId) && l.ConfirmedParcelId != null && l.Remittance.CourierId == courierId)
                .Select(l => new { l.ParcelId, l.Commission }).ToListAsync(cancellationToken))
            .Where(l => Money.ToCents(l.Commission) > 0).Select(l => l.ParcelId).ToHashSet();
        var lines = new List<ComputedLine>();
        foreach (var link in links)
        {
            if (KindOf(link.RouteKind, link.FinalStatusId) is not { } kind || settled.Contains($"{link.ParcelId}:{kind}")) continue;
            var pickup = kind == CourierStatementLineKind.PICKUP;
            var cityId = pickup ? link.OriginCityId : link.DestinationCityId;
            var fromGrid = kind switch
            {
                CourierStatementLineKind.PICKUP => pickupFee.TryGetValue(cityId, out var p) ? p : pickupFee.GetValueOrDefault(link.RouteCityId),
                CourierStatementLineKind.DELIVERY => deliveryFee.TryGetValue(cityId, out var d) ? d : deliveryFee.GetValueOrDefault(link.RouteCityId),
                _ => returnFee,
            };
            var amount = Money.ToCentsOrNull(link.CourierCommission) ?? Money.ToCentsOrNull(link.CourierFee) ?? fromGrid;
            lines.Add(new ComputedLine(link.ParcelId, kind, link.Code, pickup ? link.Origin : link.Destination, link.FinalStatusId ?? 0, amount,
                kind == CourierStatementLineKind.DELIVERY && deducted.Contains(link.ParcelId), link.ConfirmedAt ?? start));
        }
        return lines;
    }

    /// <summary>`codCollectedOver` : le COD encaissé sur les livraisons de la période, pour mémoire.</summary>
    public static async Task<long> CodCollectedAsync(IAppDbContext db, string courierId, DateTime start, DateTime end, CancellationToken cancellationToken) =>
        Money.ToCentsOrNull(await db.RouteParcels.AsNoTracking()
            .Where(l => l.Confirmed && l.ConfirmedAt >= start && l.ConfirmedAt <= end && l.FinalStatusId == Billing.DeliveredStatusId && l.Route.CourierId == courierId)
            .SumAsync(l => l.CodCollected, cancellationToken)) ?? 0;

    /// <summary>`totals` : fixe selon le mode, commissions moins ce qui est déjà gardé aux remises.</summary>
    public static Totals TotalsOf(PayModel payModel, long fixedSalary, IReadOnlyList<ComputedLine> lines)
    {
        var total = lines.Sum(l => l.Amount);
        var deducted = lines.Where(l => l.Deducted).Sum(l => l.Amount);
        var baseAmount = payModel == PayModel.COMMISSION ? 0 : fixedSalary;
        var variable = payModel == PayModel.SALARY ? 0 : total - deducted;
        return new Totals(baseAmount, total, deducted, variable, baseAmount + variable);
    }

    public static CourierStatementLine LineOf(string statementId, ComputedLine line) => new()
    {
        StatementId = statementId, ParcelId = line.ParcelId, Kind = line.Kind, ParcelCode = line.ParcelCode, CityName = line.CityName, StatusId = line.StatusId, Amount = Money.FromCents(line.Amount),
        Deducted = line.Deducted, WorkedAt = line.WorkedAt, CreatedAt = DateTime.UtcNow,
    };

    /// <summary>`recompute` : les lignes d'un brouillon refaites depuis les courses ; le fixe reste celui du relevé.</summary>
    public static async Task RecomputeAsync(IAppDbContext db, ICurrentUser caller, string code, CancellationToken cancellationToken)
    {
        var statement = await Scoped(db.CourierStatements.AsNoTracking(), caller).Where(s => s.Code == code)
                            .Select(s => new { s.Id, s.Status, s.CourierId, s.PeriodStart, s.PeriodEnd, s.PayModel, s.BaseAmount }).FirstOrDefaultAsync(cancellationToken)
                        ?? throw BusinessException.NotFound($"Relevé introuvable : {code}");
        if (statement.Status != CourierStatementStatus.DRAFT) throw BusinessException.Conflict("Ce relevé est validé : il ne se recalcule plus.", new { reason = "statement-validated" });
        var lines = await ComputeLinesAsync(db, statement.CourierId, statement.PeriodStart, statement.PeriodEnd, cancellationToken);
        var cod = await CodCollectedAsync(db, statement.CourierId, statement.PeriodStart, statement.PeriodEnd, cancellationToken);
        var totals = TotalsOf(statement.PayModel, Money.ToCents(statement.BaseAmount), lines);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.CourierStatementLines.Where(l => l.StatementId == statement.Id).ExecuteDeleteAsync(cancellationToken);
        foreach (var line in lines) db.CourierStatementLines.Add(LineOf(statement.Id, line));
        await db.SaveChangesAsync(cancellationToken);
        await db.CourierStatements.Where(s => s.Id == statement.Id).ExecuteUpdateAsync(set => set.SetProperty(s => s.CodCollected, Money.FromCents(cod))
            .SetProperty(s => s.CommissionsTotal, Money.FromCents(totals.CommissionsTotal)).SetProperty(s => s.CommissionsDeducted, Money.FromCents(totals.CommissionsDeducted))
            .SetProperty(s => s.VariableAmount, Money.FromCents(totals.Variable)).SetProperty(s => s.TotalDue, Money.FromCents(totals.Due)).SetProperty(s => s.UpdatedAt, DateTime.UtcNow), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Après l'annulation d'une livraison (`DeliveryCancelService`) : chaque relevé brouillon qui portait sa course se
    /// recalcule ; un échec n'annule pas l'annulation, le brouillon se recalcule encore à la main.
    /// </summary>
    public static async Task RecomputeDraftsForAsync(IAppDbContext db, ICurrentUser caller, ILogger logger, IReadOnlyList<string> codes, string parcelCode, CancellationToken cancellationToken)
    {
        foreach (var code in codes.Distinct())
        {
            try
            {
                await RecomputeAsync(db, new ElevatedCaller(caller), code, cancellationToken);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                db.ChangeTracker.Clear();
                logger.LogWarning("Relevé {Code} non recalculé après l’annulation de {Parcel} : {Message}", code, parcelCode, error.Message);
            }
        }
    }

    public static Task<List<string>> DraftCodesWithDeliveryAsync(IAppDbContext db, string parcelId, CancellationToken cancellationToken) =>
        db.CourierStatementLines.AsNoTracking().Where(l => l.ParcelId == parcelId && l.Kind == CourierStatementLineKind.DELIVERY && l.Statement.Status == CourierStatementStatus.DRAFT)
            .Select(l => l.Statement.Code).Distinct().ToListAsync(cancellationToken);
}
