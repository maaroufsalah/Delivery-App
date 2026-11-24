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

public class CourierStatementListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CourierId { get; set; } = string.Empty;
    public string CourierName { get; set; } = string.Empty;
    public string AgencyId { get; set; } = string.Empty;
    public string AgencyName { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string PeriodKind { get; set; } = string.Empty;
    public string PayModel { get; set; } = string.Empty;
    public string SettlementMode { get; set; } = string.Empty;
    public int LineCount { get; set; }
    public long BaseAmount { get; set; }
    public long CodCollected { get; set; }
    public long CommissionsTotal { get; set; }
    public long CommissionsDeducted { get; set; }
    public long VariableAmount { get; set; }
    public long TotalDue { get; set; }
    public DateTime? ValidatedAt { get; set; }
    public string? ValidatedByName { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed record CourierStatementLineDto(string Id, string ParcelId, string Kind, string ParcelCode, string CityName, int StatusId, long Amount, bool Deducted, DateTime WorkedAt);

public sealed class CourierStatementDetailDto : CourierStatementListItemDto
{
    [JsonPropertyOrder(1)] public string? Note { get; set; }
    [JsonPropertyOrder(1)] public bool Validatable { get; set; }
    [JsonPropertyOrder(1)] public string PaymentMethod { get; set; } = string.Empty;
    [JsonPropertyOrder(1)] public string? BankAccountId { get; set; }
    [JsonPropertyOrder(1)] public string? BankAccountLabel { get; set; }
    [JsonPropertyOrder(1)] public string? Reference { get; set; }
    [JsonPropertyOrder(1)] public string? ChequeNumber { get; set; }
    [JsonPropertyOrder(1)] public string? ChequeBank { get; set; }
    [JsonPropertyOrder(1)] public DateTime? ChequeDate { get; set; }
    [JsonPropertyOrder(1)] public string? ProofName { get; set; }
    [JsonPropertyOrder(1)] public List<CourierStatementLineDto> Lines { get; set; } = [];
}

/// <summary>`CourierCheque` : un relevé réglé par chèque, vu du livreur.</summary>
public sealed record CourierChequeDto(string Id, string StatementCode, string Number, string? Bank, DateTime? IssuedOn, long AmountInCents, bool HandedOver, bool HasPhoto);

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

/// <summary>`statementListQuerySchema`.</summary>
public sealed record StatementListFilter(ListQuery Paging, string? Status, string? CourierId, string? AgencyId, string? From, string? To);

internal static class StatementLists
{
    public static readonly string[] Statuses = ["draft", "validated", "paid"];

    public static IQueryable<CourierStatement> Filter(IAppDbContext db, ICurrentUser caller, StatementListFilter filter)
    {
        if (filter.Status is { } s && !Statuses.Contains(s))
        {
            throw BusinessException.Validation("Saisie invalide.", new { issues = new[] { new { path = "status", message = Zod.InvalidOption(Statuses) } } });
        }
        var where = CourierStatements.Scoped(db.CourierStatements.AsNoTracking(), caller);
        if (filter.Status is { } status) { var value = Enum.Parse<CourierStatementStatus>(status.ToUpperInvariant()); where = where.Where(x => x.Status == value); }
        if (!string.IsNullOrEmpty(filter.CourierId) && caller.MayFilterBy(caller.CourierId)) where = where.Where(x => x.CourierId == filter.CourierId);
        if (!string.IsNullOrEmpty(filter.AgencyId) && caller.MayFilterBy(caller.AgencyId)) where = where.Where(x => x.AgencyId == filter.AgencyId);
        if (!string.IsNullOrEmpty(filter.From)) { var from = Day(filter.From); where = where.Where(x => x.PeriodEnd >= from); }
        if (!string.IsNullOrEmpty(filter.To)) { var to = Day(filter.To).AddDays(1).AddMilliseconds(-1); where = where.Where(x => x.PeriodStart <= to); }
        if (filter.Paging.Search is { Length: > 0 } search)
        {
            var needle = search.ToLower();
            where = where.Where(x => x.Code.ToLower().Contains(needle) || x.Courier.LastName.ToLower().Contains(needle));
        }
        return filter.Paging.Order == "asc" ? where.OrderBy(x => x.PeriodStart) : where.OrderByDescending(x => x.PeriodStart);
    }

    public static DateTime Day(string day) => DateTime.TryParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date)
        ? date
        : throw BusinessException.Validation("Saisie invalide.", new { issues = new[] { new { path = "from", message = "Invalid date" } } });
}

/// <summary>`GET /courier-statements`.</summary>
public sealed record GetCourierStatementsQuery(StatementListFilter Filter) : IRequest<Paginated<CourierStatementListItemDto>>;

internal sealed class GetCourierStatementsHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetCourierStatementsQuery, Paginated<CourierStatementListItemDto>>
{
    public async Task<Paginated<CourierStatementListItemDto>> Handle(GetCourierStatementsQuery request, CancellationToken cancellationToken)
    {
        var where = StatementLists.Filter(db, caller, request.Filter);
        var paging = request.Filter.Paging;
        var total = await where.CountAsync(cancellationToken);
        var items = (await CourierStatements.Project(where.Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize)).ToListAsync(cancellationToken)).Select(CourierStatements.Wire).ToList();
        return new Paginated<CourierStatementListItemDto>(items, total, paging.Page, paging.PageSize);
    }
}

/// <summary>`GET /courier-statements/export` : classeur « Relevés livreur ».</summary>
public sealed record ExportCourierStatementsQuery(StatementListFilter Filter) : IRequest<FileContent>;

internal sealed class ExportCourierStatementsHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportCourierStatementsQuery, FileContent>
{
    private static readonly IReadOnlyList<ExportColumn<CourierStatementListItemDto>> Columns =
    [
        new("Relevé", r => r.Code),
        new("Statut", r => DocumentLabels.Enum(r.Status)),
        new("Livreur", r => r.CourierName),
        new("Agence", r => r.AgencyName),
        new("Début", r => Exports.CalendarDateText(DateOnly.FromDateTime(r.PeriodStart))),
        new("Fin", r => Exports.CalendarDateText(DateOnly.FromDateTime(r.PeriodEnd))),
        new("Mode", r => DocumentLabels.Enum(r.PayModel)),
        new("Règlement des commissions", r => DocumentLabels.Enum(r.SettlementMode)),
        new("Courses", r => r.LineCount),
        new("Fixe", r => Exports.Amount(r.BaseAmount)),
        new("COD encaissé", r => Exports.Amount(r.CodCollected)),
        new("Commissions", r => Exports.Amount(r.CommissionsTotal)),
        new("Déjà gardées aux remises", r => Exports.Amount(r.CommissionsDeducted)),
        new("Reste dû sur les courses", r => Exports.Amount(r.VariableAmount)),
        new("Net à payer", r => Exports.Amount(r.TotalDue)),
        new("Validé le", r => Exports.DateTimeText(r.ValidatedAt)),
        new("Payé le", r => Exports.DateTimeText(r.PaidAt)),
    ];

    public async Task<FileContent> Handle(ExportCourierStatementsQuery request, CancellationToken cancellationToken)
    {
        var where = StatementLists.Filter(db, caller, request.Filter);
        Exports.AssertWithinLimit(await where.CountAsync(cancellationToken));
        var rows = (await CourierStatements.Project(where).ToListAsync(cancellationToken)).Select(CourierStatements.Wire).ToList();
        return await Exports.ToXlsxAsync(db, excel, clock, caller, "Relevés livreur", Columns, rows, cancellationToken);
    }
}

/// <summary>`GET /courier-statements/:code`.</summary>
public sealed record GetCourierStatementQuery(string Code) : IRequest<CourierStatementDetailDto>;

internal sealed class GetCourierStatementHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetCourierStatementQuery, CourierStatementDetailDto>
{
    public Task<CourierStatementDetailDto> Handle(GetCourierStatementQuery request, CancellationToken cancellationToken) => CourierStatements.DetailAsync(db, caller, request.Code, cancellationToken);
}

/// <summary>`courierStatementSchema` : le relevé d'un livreur sur une période, ses courses calculées.</summary>
public sealed record CreateCourierStatementCommand : IRequest<CourierStatementDetailDto>
{
    public string? CourierId { get; init; }
    public string? AgencyId { get; init; }
    public string? PeriodStart { get; init => field = Zod.Trim(value); }
    public string? PeriodEnd { get; init => field = Zod.Trim(value); }
    public string? Note { get; init => field = Zod.Trim(value); }
}

public sealed class CreateCourierStatementValidator : ZodValidator<CreateCourierStatementCommand>
{
    public CreateCourierStatementValidator()
    {
        RuleFor(x => x.CourierId).Required().MinLength(1, "required");
        RuleFor(x => x.AgencyId).Required().MinLength(1, "required");
        RuleFor(x => x.PeriodStart).Required().Matches(@"^\d{4}-\d{2}-\d{2}$").WithMessage("invalid-date");
        RuleFor(x => x.PeriodEnd).Required().Matches(@"^\d{4}-\d{2}-\d{2}$").WithMessage("invalid-date");
        RuleFor(x => x.Note).MaxLength(500);
    }

    protected override void Refine(CreateCourierStatementCommand value, Action<string, string> issue)
    {
        if (value.PeriodStart is { } a && value.PeriodEnd is { } b && string.CompareOrdinal(a, b) > 0) issue(ValidationPaths.Raw + "periodEnd", "period-reversed");
    }
}

internal sealed class CreateCourierStatementHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<CreateCourierStatementCommand, CourierStatementDetailDto>
{
    public async Task<CourierStatementDetailDto> Handle(CreateCourierStatementCommand input, CancellationToken cancellationToken)
    {
        await ReturnRows.AssertAgencyReachableAsync(db, caller, input.AgencyId!, cancellationToken);
        var courier = await db.Couriers.AsNoTracking().Where(c => c.Id == input.CourierId && c.DeletedAt == null)
                          .Select(c => new { c.Id, c.PayModel, c.PayPeriod, c.FixedSalary, c.SettlementMode }).FirstOrDefaultAsync(cancellationToken)
                      ?? throw BusinessException.NotFound("Livreur introuvable.");
        var start = StatementLists.Day(input.PeriodStart!);
        var end = StatementLists.Day(input.PeriodEnd!).AddDays(1).AddMilliseconds(-1);
        var lines = await CourierStatements.ComputeLinesAsync(db, courier.Id, start, end, cancellationToken);
        var cod = await CourierStatements.CodCollectedAsync(db, courier.Id, start, end, cancellationToken);
        var totals = CourierStatements.TotalsOf(courier.PayModel, Money.ToCents(courier.FixedSalary ?? 0m), lines);
        var prefix = await db.SettingAsync(SettingKeys.StatementPrefix, cancellationToken) is { ValueKind: JsonValueKind.String } p ? p.GetString()! : CourierStatements.DefaultPrefix;
        var yearly = await db.BoolSettingAsync(SettingKeys.NumberingYearlyReset, false, cancellationToken);
        var now = DateTime.UtcNow;
        string code;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            code = await DocumentCodes.ReserveAsync(db, prefix, now, cancellationToken, yearly);
            var statement = new CourierStatement
            {
                Code = code, CourierId = courier.Id, AgencyId = input.AgencyId!, PeriodStart = start, PeriodEnd = end, PeriodKind = courier.PayPeriod, PayModel = courier.PayModel,
                SettlementMode = courier.SettlementMode, BaseAmount = Money.FromCents(totals.Base), CodCollected = Money.FromCents(cod), CommissionsTotal = Money.FromCents(totals.CommissionsTotal),
                CommissionsDeducted = Money.FromCents(totals.CommissionsDeducted), VariableAmount = Money.FromCents(totals.Variable), TotalDue = Money.FromCents(totals.Due),
                Note = ReturnRows.Text(input.Note), CreatedById = caller.UserId, CreatedAt = now, UpdatedAt = now,
            };
            db.CourierStatements.Add(statement);
            foreach (var line in lines) db.CourierStatementLines.Add(CourierStatements.LineOf(statement.Id, line));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        return await CourierStatements.DetailAsync(db, caller, code, cancellationToken);
    }
}

/// <summary>`POST /courier-statements/:code/recompute`.</summary>
public sealed record RecomputeCourierStatementCommand(string Code) : IRequest<CourierStatementDetailDto>;

internal sealed class RecomputeCourierStatementHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<RecomputeCourierStatementCommand, CourierStatementDetailDto>
{
    public async Task<CourierStatementDetailDto> Handle(RecomputeCourierStatementCommand request, CancellationToken cancellationToken)
    {
        await CourierStatements.RecomputeAsync(db, caller, request.Code, cancellationToken);
        return await CourierStatements.DetailAsync(db, caller, request.Code, cancellationToken);
    }
}

/// <summary>`POST /courier-statements/:code/validate` : chaque course est marquée payée ; une course ne se paie qu'une fois.</summary>
public sealed record ValidateCourierStatementCommand(string Code) : IRequest<CourierStatementDetailDto>;

internal sealed class ValidateCourierStatementHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<ValidateCourierStatementCommand, CourierStatementDetailDto>
{
    public async Task<CourierStatementDetailDto> Handle(ValidateCourierStatementCommand request, CancellationToken cancellationToken)
    {
        var statement = await CourierStatements.Scoped(db.CourierStatements.AsNoTracking(), caller).Where(s => s.Code == request.Code)
                            .Select(s => new { s.Id, s.Status, s.AgencyId, Lines = s.Lines.Select(l => new { l.Id, l.ParcelId }).ToList() }).FirstOrDefaultAsync(cancellationToken)
                        ?? throw BusinessException.NotFound($"Relevé introuvable : {request.Code}");
        if (statement.Status != CourierStatementStatus.DRAFT) throw BusinessException.Conflict("Ce relevé est déjà validé.", new { reason = "statement-already-validated" });
        await ReturnRows.AssertAgencyReachableAsync(db, caller, statement.AgencyId, cancellationToken);
        var now = DateTime.UtcNow;
        var userId = caller.UserId;
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            foreach (var line in statement.Lines)
            {
                await db.CourierStatementLines.Where(l => l.Id == line.Id).ExecuteUpdateAsync(set => set.SetProperty(l => l.SettledParcelId, line.ParcelId), cancellationToken);
            }
            await db.CourierStatements.Where(s => s.Id == statement.Id).ExecuteUpdateAsync(set => set.SetProperty(s => s.Status, CourierStatementStatus.VALIDATED).SetProperty(s => s.ValidatedAt, now)
                .SetProperty(s => s.ValidatedById, userId).SetProperty(s => s.UpdatedAt, now), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Npgsql.PostgresException error) when (error.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation)
        {
            throw BusinessException.Conflict("Une course de ce relevé vient d’être payée ailleurs. Recalculez le brouillon.", new { reason = "course-already-settled" });
        }
        return await CourierStatements.DetailAsync(db, caller, request.Code, cancellationToken);
    }
}

/// <summary>`statementPaySchema` : le règlement du relevé validé (espèces, virement ou chèque).</summary>
public sealed record PayCourierStatementCommand : IRequest<CourierStatementDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public string? Method { get; init; }
    public string? BankAccountId { get; init => field = Zod.Trim(value); }
    public string? Reference { get; init => field = Zod.Trim(value); }
    public string? ChequeNumber { get; init => field = Zod.Trim(value); }
    public string? ChequeBank { get; init => field = Zod.Trim(value); }
    public string? ChequeDate { get; init => field = Zod.Trim(value); }
    public ProofFileDto? Proof { get; init; }
}

public sealed class PayCourierStatementValidator : ZodValidator<PayCourierStatementCommand>
{
    public PayCourierStatementValidator()
    {
        RuleFor(x => x.Method).OneOf(["cash", "transfer", "check"]);
        RuleFor(x => x.Reference).MaxLength(120);
        RuleFor(x => x.ChequeNumber).MaxLength(40);
        RuleFor(x => x.ChequeBank).MaxLength(80);
        RuleFor(x => x.ChequeDate).Matches(@"^\d{4}-\d{2}-\d{2}$").WithMessage("invalid-date").When(x => !string.IsNullOrEmpty(x.ChequeDate));
        ProofRules.Proof(this, x => x.Proof);
    }

    protected override void Refine(PayCourierStatementCommand value, Action<string, string> issue)
    {
        var method = value.Method ?? "cash";
        if (method == "check" && string.IsNullOrEmpty(value.ChequeNumber)) issue(ValidationPaths.Raw + "chequeNumber", "cheque-number-required");
        if (method != "cash" && string.IsNullOrEmpty(value.BankAccountId)) issue(ValidationPaths.Raw + "bankAccountId", "bank-account-required");
    }
}

internal sealed class PayCourierStatementHandler(IAppDbContext db, ICurrentUser caller, IFileStorage storage) : IRequestHandler<PayCourierStatementCommand, CourierStatementDetailDto>
{
    public async Task<CourierStatementDetailDto> Handle(PayCourierStatementCommand input, CancellationToken cancellationToken)
    {
        var statement = await CourierStatements.Scoped(db.CourierStatements.AsNoTracking(), caller).Where(s => s.Code == input.Code).Select(s => new { s.Id, s.Status, s.AgencyId })
                            .FirstOrDefaultAsync(cancellationToken) ?? throw BusinessException.NotFound($"Relevé introuvable : {input.Code}");
        if (statement.Status == CourierStatementStatus.DRAFT) throw BusinessException.Conflict("Ce relevé n’est pas encore validé.", new { reason = "statement-draft" });
        if (statement.Status == CourierStatementStatus.PAID) throw BusinessException.Conflict("Ce relevé est déjà payé.", new { reason = "statement-already-paid" });
        await ReturnRows.AssertAgencyReachableAsync(db, caller, statement.AgencyId, cancellationToken);
        if (!string.IsNullOrEmpty(input.BankAccountId) && !await db.BankAccounts.AnyAsync(a => a.Id == input.BankAccountId && a.DeletedAt == null, cancellationToken))
        {
            throw BusinessException.NotFound("Compte bancaire introuvable.");
        }
        var proof = input.Proof is null ? null : await storage.PutAsync($"cheques/{caller.CompanyId}", Convert.FromBase64String(input.Proof.Content!), input.Proof.MimeType!, cancellationToken);
        var row = await db.CourierStatements.FirstAsync(s => s.Id == statement.Id, cancellationToken);
        row.Status = CourierStatementStatus.PAID;
        row.PaidAt = DateTime.UtcNow;
        row.PaidById = caller.UserId;
        row.PaymentMethod = Enum.Parse<PaymentMethod>((input.Method ?? "cash").ToUpperInvariant());
        row.BankAccountId = ReturnRows.Text(input.BankAccountId);
        row.ChequeNumber = ReturnRows.Text(input.ChequeNumber);
        row.ChequeBank = ReturnRows.Text(input.ChequeBank);
        row.Reference = ReturnRows.Text(input.Reference);
        row.ChequeDate = string.IsNullOrEmpty(input.ChequeDate) ? null : PettyCashDays.Parse(input.ChequeDate);
        row.UpdatedAt = DateTime.UtcNow;
        if (proof is not null) { row.ProofName = input.Proof!.Name; row.ProofMimeType = input.Proof.MimeType; row.ProofPath = proof.Key; }
        await db.SaveChangesAsync(cancellationToken);
        return await CourierStatements.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}

/// <summary>`chequePhotoSchema` : la photo du chèque d'un relevé réglé ; l'ancienne est effacée.</summary>
public sealed record AttachStatementChequePhotoCommand : IRequest<CourierStatementDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public ProofFileDto? Proof { get; init; }
}

public sealed class AttachStatementChequePhotoValidator : ZodValidator<AttachStatementChequePhotoCommand>
{
    public AttachStatementChequePhotoValidator()
    {
        RuleFor(x => x.Proof).Required("object");
        ProofRules.Proof(this, x => x.Proof);
    }
}

internal sealed class AttachStatementChequePhotoHandler(IAppDbContext db, ICurrentUser caller, IFileStorage storage) : IRequestHandler<AttachStatementChequePhotoCommand, CourierStatementDetailDto>
{
    public async Task<CourierStatementDetailDto> Handle(AttachStatementChequePhotoCommand input, CancellationToken cancellationToken)
    {
        var statement = await CourierStatements.Scoped(db.CourierStatements.AsNoTracking(), caller).Where(s => s.Code == input.Code).Select(s => new { s.Id, s.Status, s.ProofPath })
                            .FirstOrDefaultAsync(cancellationToken) ?? throw BusinessException.NotFound($"Relevé introuvable : {input.Code}");
        if (statement.Status != CourierStatementStatus.PAID)
        {
            throw BusinessException.Conflict("Ce relevé n’est pas encore réglé : il n’y a pas de chèque à joindre.", new { reason = "statement-not-paid" });
        }
        var stored = await storage.PutAsync($"cheques/{caller.CompanyId}", Convert.FromBase64String(input.Proof!.Content!), input.Proof.MimeType!, cancellationToken);
        await db.CourierStatements.Where(s => s.Id == statement.Id).ExecuteUpdateAsync(set => set.SetProperty(s => s.ProofName, input.Proof.Name)
            .SetProperty(s => s.ProofMimeType, input.Proof.MimeType).SetProperty(s => s.ProofPath, stored.Key), cancellationToken);
        if (statement.ProofPath is not null) await storage.RemoveAsync(statement.ProofPath, cancellationToken);
        return await CourierStatements.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}

/// <summary>`GET /courier-statements/:code/cheque-photo` : l'image se regarde (`inline`).</summary>
public sealed record GetStatementChequePhotoQuery(string Code) : IRequest<FileContent>;

internal sealed class GetStatementChequePhotoHandler(IAppDbContext db, ICurrentUser caller, IFileStorage storage) : IRequestHandler<GetStatementChequePhotoQuery, FileContent>
{
    public async Task<FileContent> Handle(GetStatementChequePhotoQuery request, CancellationToken cancellationToken)
    {
        var statement = await CourierStatements.Scoped(db.CourierStatements.AsNoTracking(), caller).Where(s => s.Code == request.Code).Select(s => new { s.ProofName, s.ProofMimeType, s.ProofPath })
                            .FirstOrDefaultAsync(cancellationToken) ?? throw BusinessException.NotFound($"Relevé introuvable : {request.Code}");
        if (statement.ProofPath is null) throw BusinessException.NotFound("Aucune photo de chèque sur ce relevé.", new { reason = "no-cheque-photo" });
        return new FileContent(await storage.GetAsync(statement.ProofPath, cancellationToken), statement.ProofMimeType ?? "application/octet-stream", statement.ProofName ?? "cheque", Inline: true,
            CacheControl: "private, no-store");
    }
}

/// <summary>`DELETE /courier-statements/:code` : un brouillon seulement.</summary>
public sealed record DeleteCourierStatementCommand(string Code) : IRequest<ReturnCodeDto>;

internal sealed class DeleteCourierStatementHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<DeleteCourierStatementCommand, ReturnCodeDto>
{
    public async Task<ReturnCodeDto> Handle(DeleteCourierStatementCommand request, CancellationToken cancellationToken)
    {
        var statement = await CourierStatements.Scoped(db.CourierStatements.AsNoTracking(), caller).Where(s => s.Code == request.Code).Select(s => new { s.Id, s.Status })
                            .FirstOrDefaultAsync(cancellationToken) ?? throw BusinessException.NotFound($"Relevé introuvable : {request.Code}");
        if (statement.Status != CourierStatementStatus.DRAFT) throw BusinessException.Conflict("Un relevé validé ne se supprime pas.", new { reason = "statement-validated" });
        await db.CourierStatements.Where(s => s.Id == statement.Id).ExecuteDeleteAsync(cancellationToken);
        return new ReturnCodeDto(request.Code);
    }
}

/// <summary>`GET /courier-statements/:code/pdf` : le relevé de rémunération.</summary>
public sealed record GetCourierStatementPdfQuery(string Code) : IRequest<FileContent>;

internal sealed class GetCourierStatementPdfHandler(IAppDbContext db, ICurrentUser caller, IPdfService pdf) : IRequestHandler<GetCourierStatementPdfQuery, FileContent>
{
    public async Task<FileContent> Handle(GetCourierStatementPdfQuery request, CancellationToken cancellationToken)
    {
        var s = await CourierStatements.DetailAsync(db, caller, request.Code, cancellationToken);
        var company = await PdfCompany.LoadAsync(db, "Relevé", cancellationToken);
        var sheet = new CourierStatementSheet(company, s.Code, s.Status, s.AgencyName, s.CourierName, s.PayModel, s.SettlementMode, DateOnly.FromDateTime(s.PeriodStart),
            DateOnly.FromDateTime(s.PeriodEnd), s.LineCount, s.BaseAmount, s.CodCollected, s.CommissionsTotal, s.CommissionsDeducted, s.VariableAmount, s.TotalDue, s.ValidatedByName,
            s.Lines.Select(l => new CourierStatementSheetLine(l.WorkedAt, l.ParcelCode, l.Kind, l.CityName, l.Deducted, l.Amount)).ToList());
        return new FileContent(pdf.Render(sheet), FileContent.Pdf, $"{s.Code}.pdf", CacheControl: "private, no-store");
    }
}

/// <summary>`GET /courier/me/cheques` : ses relevés réglés par chèque, les 100 derniers.</summary>
public sealed record GetMyChequesQuery : IRequest<IReadOnlyList<CourierChequeDto>>;

internal sealed class GetMyChequesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetMyChequesQuery, IReadOnlyList<CourierChequeDto>>
{
    public async Task<IReadOnlyList<CourierChequeDto>> Handle(GetMyChequesQuery request, CancellationToken cancellationToken)
    {
        var courierId = caller.CourierId ?? throw BusinessException.Forbidden("Cet espace est réservé aux livreurs.", new { reason = "not-a-courier" });
        var rows = await db.CourierStatements.AsNoTracking().Where(s => s.CourierId == courierId && s.PaymentMethod == PaymentMethod.CHECK && s.ChequeNumber != null)
            .OrderByDescending(s => s.CreatedAt).Take(100).Select(s => new { s.Id, s.Code, s.ChequeNumber, s.ChequeBank, s.ChequeDate, s.TotalDue, s.PaidAt, s.ProofPath })
            .ToListAsync(cancellationToken);
        return rows.Select(s => new CourierChequeDto(s.Id, s.Code, s.ChequeNumber ?? string.Empty, s.ChequeBank,
            s.ChequeDate is { } d ? DateTime.SpecifyKind(d.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc) : null, Money.ToCents(s.TotalDue), s.PaidAt is not null, s.ProofPath is not null)).ToList();
    }
}

/// <summary>`courierChequeSchema` : le livreur photographie le chèque de son dernier relevé réglé par chèque.</summary>
public sealed record DepositMyChequeCommand : IRequest<CourierStatementDetailDto>
{
    public string? ChequeNumber { get; init => field = Zod.Trim(value); }
    public string? ChequeBank { get; init => field = Zod.Trim(value); }
    public string? ChequeDate { get; init => field = Zod.Trim(value); }
    public double? Amount { get; init; }
    public ProofFileDto? Proof { get; init; }
}

public sealed class DepositMyChequeValidator : ZodValidator<DepositMyChequeCommand>
{
    private static readonly string[] Images = ["image/jpeg", "image/png", "image/webp"];

    public DepositMyChequeValidator()
    {
        RuleFor(x => x.ChequeNumber).Required().MinLength(1, "required").MaxLength(40);
        RuleFor(x => x.ChequeBank).MaxLength(80);
        RuleFor(x => x.ChequeDate).Matches(@"^\d{4}-\d{2}-\d{2}$").WithMessage("invalid-date").When(x => !string.IsNullOrEmpty(x.ChequeDate));
        RuleFor(x => x.Amount).Must(v => v is null || v.Value >= 0).WithMessage("negative").Must(v => v is null || v.Value <= 1_000_000).WithMessage("amount-too-large");
        RuleFor(x => x.Proof).Required("object");
        RuleFor(x => x.Proof!.Name).Required().MinLength(1, "required").MaxLength(120).OverridePropertyName("proof.name").When(x => x.Proof is not null);
        RuleFor(x => x.Proof!.MimeType).Must(v => v is not null && Images.Contains(v)).WithMessage("unsupported-file-type").OverridePropertyName("proof.mimeType").When(x => x.Proof is not null);
        RuleFor(x => x.Proof!.Content).Required().MinLength(1, "required").OverridePropertyName("proof.content").When(x => x.Proof is not null);
    }
}

internal sealed class DepositMyChequeHandler(IAppDbContext db, ICurrentUser caller, IFileStorage storage) : IRequestHandler<DepositMyChequeCommand, CourierStatementDetailDto>
{
    public async Task<CourierStatementDetailDto> Handle(DepositMyChequeCommand input, CancellationToken cancellationToken)
    {
        var courierId = caller.CourierId ?? throw BusinessException.Forbidden("Cet espace est réservé aux livreurs.", new { reason = "not-a-courier" });
        var statement = await db.CourierStatements.Where(s => s.CourierId == courierId && s.Status == CourierStatementStatus.PAID && s.PaymentMethod == PaymentMethod.CHECK)
                            .OrderByDescending(s => s.PaidAt).FirstOrDefaultAsync(cancellationToken)
                        ?? throw BusinessException.NotFound("Aucun relevé réglé par chèque à documenter. Demandez à votre agence.", new { reason = "no-cheque-statement" });
        var previous = statement.ProofPath;
        var stored = await storage.PutAsync($"cheques/{caller.CompanyId}", Convert.FromBase64String(input.Proof!.Content!), input.Proof.MimeType!, cancellationToken);
        statement.ProofName = input.Proof.Name;
        statement.ProofMimeType = input.Proof.MimeType;
        statement.ProofPath = stored.Key;
        statement.ChequeNumber ??= input.ChequeNumber;
        statement.ChequeBank ??= ReturnRows.Text(input.ChequeBank);
        statement.ChequeDate ??= string.IsNullOrEmpty(input.ChequeDate) ? null : PettyCashDays.Parse(input.ChequeDate);
        statement.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        if (previous is not null) await storage.RemoveAsync(previous, cancellationToken);
        return await CourierStatements.DetailAsync(db, caller, statement.Code, cancellationToken);
    }
}
