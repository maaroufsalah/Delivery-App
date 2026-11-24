using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Idempotency;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.PettyCash;

/// <summary>`PettyCashService` : portée, catégories, liste et projection d'un mouvement.</summary>
public static class PettyCashMovements
{
    public const string CategoriesSetting = "pettyCash.categories";
    public static readonly string[] DefaultCategories = ["Fournitures", "Transport", "Entretien", "Autre"];
    public static readonly string[] Kinds = ["supply", "expense"];
    public static readonly string[] Statuses = ["draft", "validated", "refused"];
    private const int PdfMaxRows = 5_000;

    /// <summary>`baseWhere` : administrateur tout (ou l'agence demandée), agent son agence seulement.</summary>
    public static IQueryable<PettyCashMovement> Scoped(IQueryable<PettyCashMovement> movements, ICurrentUser caller, string? agencyId = null)
    {
        if (caller.IsSuperAdmin()) return string.IsNullOrEmpty(agencyId) ? movements : movements.Where(m => m.AgencyId == agencyId);
        if (caller.ClientId is not null || caller.CourierId is not null || caller.AgencyId is null) return movements.Where(m => m.Id == Scope.DenyAllId);
        if (!string.IsNullOrEmpty(agencyId) && agencyId != caller.AgencyId) return movements.Where(m => m.Id == Scope.DenyAllId);
        var own = caller.AgencyId;
        return movements.Where(m => m.AgencyId == own);
    }

    /// <summary>`buyerWhere` : l'équipe active (administrateurs et personnel), de l'agence de l'agent.</summary>
    public static IQueryable<User> Buyers(IAppDbContext db, ICurrentUser caller)
    {
        var users = db.Users.AsNoTracking().Where(u => u.IsActive && u.DeletedAt == null && u.Roles.Any(r => r.Role == RoleName.SUPER_ADMIN || r.Role == RoleName.STAFF));
        if (!caller.IsSuperAdmin() && caller.AgencyId is { } agency) users = users.Where(u => u.AgencyId == agency);
        return users;
    }

    public static async Task<IReadOnlyList<string>> CategoriesAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        var stored = await db.SettingAsync(CategoriesSetting, cancellationToken);
        if (stored is { ValueKind: System.Text.Json.JsonValueKind.Array } list && list.GetArrayLength() > 0 && list.EnumerateArray().All(e => e.ValueKind == System.Text.Json.JsonValueKind.String))
        {
            return list.EnumerateArray().Select(e => e.GetString()!).ToList();
        }
        return DefaultCategories;
    }

    private sealed record Row(string Id, PettyCashKind Kind, DateTime OccurredAt, decimal Amount, string AgencyId, string AgencyName, string? Label, string? Category, string? BuyerId,
        string? BuyerName, string? Note, string? ProofName, string? CreatedByName, DateTime CreatedAt, DateTime UpdatedAt, PettyCashStatus Status, string? RefusalReason, string? DecidedByName,
        DateTime? DecidedAt);

    private static IQueryable<Row> Rows(IQueryable<PettyCashMovement> movements) => movements.Select(m => new Row(m.Id, m.Kind, m.OccurredAt, m.Amount, m.AgencyId, m.Agency.Name, m.Label,
        m.Category, m.BuyerId, m.BuyerName, m.Note, m.ProofName, m.CreatedBy != null ? m.CreatedBy.FirstName + " " + m.CreatedBy.LastName : null, m.CreatedAt, m.UpdatedAt, m.Status,
        m.RefusalReason, m.DecidedBy != null ? m.DecidedBy.FirstName + " " + m.DecidedBy.LastName : null, m.DecidedAt));

    private static PettyCashMovementDto ToDto(Row r, string? dayStatus) => new(r.Id, r.Kind == PettyCashKind.SUPPLY ? "supply" : "expense", PettyCashDays.DayOf(r.OccurredAt),
        Money.ToCents(r.Amount), r.AgencyId, r.AgencyName, r.Label, r.Category, r.BuyerId, r.BuyerName, r.Note, r.ProofName, r.CreatedByName, r.CreatedAt, r.UpdatedAt, Money.ToWire(r.Status),
        r.RefusalReason, r.DecidedByName, r.DecidedAt, dayStatus ?? "open");

    public static async Task<List<PettyCashMovementDto>> ProjectAsync(IAppDbContext db, IQueryable<PettyCashMovement> movements, CancellationToken cancellationToken)
    {
        var rows = await Rows(movements).ToListAsync(cancellationToken);
        var statuses = await PettyCashDays.StatusesAsync(db, rows.Select(r => (r.AgencyId, PettyCashDays.DayOf(r.OccurredAt))).ToList(), cancellationToken);
        return rows.Select(r => ToDto(r, statuses.GetValueOrDefault($"{r.AgencyId}|{PettyCashDays.DayOf(r.OccurredAt)}"))).ToList();
    }

    public static async Task<PettyCashMovementDto> OneAsync(IAppDbContext db, IQueryable<PettyCashMovement> scoped, string id, string notFound, CancellationToken cancellationToken)
    {
        var rows = await ProjectAsync(db, scoped.Where(m => m.Id == id), cancellationToken);
        return rows.FirstOrDefault() ?? throw BusinessException.NotFound(notFound);
    }

    public static async Task<PettyCashPageDto> ListAsync(IAppDbContext db, ICurrentUser caller, IClock clock, PettyCashFilter filter, CancellationToken cancellationToken)
    {
        var issues = new List<object>();
        if (!Periods.IsPeriod(filter.Period)) issues.Add(new { path = "period", message = Zod.InvalidOption(Periods.All) });
        if (filter.Kind is { } k && !Kinds.Contains(k)) issues.Add(new { path = "kind", message = Zod.InvalidOption(Kinds) });
        if (filter.Status?.Any(s => !Statuses.Contains(s)) == true) issues.Add(new { path = "status", message = Zod.InvalidOption(Statuses) });
        if (issues.Count > 0) throw BusinessException.Validation("Saisie invalide.", new { issues });
        var baseWhere = Scoped(db.PettyCashMovements.AsNoTracking(), caller, filter.AgencyId);
        var timeZone = await db.Companies.AsNoTracking().OrderBy(c => c.CreatedAt).Select(c => c.Timezone).FirstOrDefaultAsync(cancellationToken);
        var first = await baseWhere.OrderBy(m => m.OccurredAt).Select(m => (DateTime?)m.OccurredAt).FirstOrDefaultAsync(cancellationToken);
        var window = Periods.Resolve(filter.Period, filter.From, filter.To, Periods.Zone(string.IsNullOrEmpty(timeZone) ? Periods.DefaultTimeZone : timeZone), first, clock.UtcNow);
        var where = baseWhere.Where(m => m.OccurredAt >= window.Start && m.OccurredAt < window.End);
        if (filter.Kind is { } kind) { var value = kind == "supply" ? PettyCashKind.SUPPLY : PettyCashKind.EXPENSE; where = where.Where(m => m.Kind == value); }
        if (filter.Category is { } categories) where = where.Where(m => m.Category != null && categories.Contains(m.Category));
        if (filter.BuyerId is { } buyers) where = where.Where(m => m.BuyerId != null && buyers.Contains(m.BuyerId));
        if (filter.Status is { } statuses) { var values = statuses.Select(s => Enum.Parse<PettyCashStatus>(Money.FromWire(s))).ToList(); where = where.Where(m => values.Contains(m.Status)); }
        if (filter.Paging.Search is { Length: > 0 } search)
        {
            var needle = search.ToLower();
            where = where.Where(m => (m.Label != null && m.Label.ToLower().Contains(needle)) || (m.Note != null && m.Note.ToLower().Contains(needle))
                                     || (m.Category != null && m.Category.ToLower().Contains(needle)) || (m.BuyerName != null && m.BuyerName.ToLower().Contains(needle)));
        }
        var paging = filter.Paging;
        var total = await where.CountAsync(cancellationToken);
        var ordered = paging.Order == "asc" ? where.OrderBy(m => m.OccurredAt).ThenBy(m => m.CreatedAt) : where.OrderByDescending(m => m.OccurredAt).ThenByDescending(m => m.CreatedAt);
        var items = await ProjectAsync(db, ordered.Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize), cancellationToken);
        var period = await where.Where(m => m.Status == PettyCashStatus.VALIDATED).GroupBy(m => m.Kind).Select(g => new { g.Key, Sum = g.Sum(m => m.Amount) }).ToListAsync(cancellationToken);
        var all = await baseWhere.Where(m => m.Status == PettyCashStatus.VALIDATED).GroupBy(m => m.Kind).Select(g => new { g.Key, Sum = g.Sum(m => m.Amount) }).ToListAsync(cancellationToken);
        var drafts = await baseWhere.CountAsync(m => m.Status == PettyCashStatus.DRAFT, cancellationToken);
        long Sum(IEnumerable<(PettyCashKind Kind, decimal Sum)> rows, PettyCashKind kind) => Money.ToCents(rows.FirstOrDefault(r => r.Kind == kind).Sum);
        var periodRows = period.Select(p => (p.Key, p.Sum)).ToList();
        var allRows = all.Select(p => (p.Key, p.Sum)).ToList();
        return new PettyCashPageDto(items, total, paging.Page, paging.PageSize,
            new PettyCashTotalsDto(Sum(allRows, PettyCashKind.SUPPLY) - Sum(allRows, PettyCashKind.EXPENSE), Sum(periodRows, PettyCashKind.SUPPLY), Sum(periodRows, PettyCashKind.EXPENSE), drafts),
            window.Period, window.From, window.To);
    }

    /// <summary>Toutes les lignes validées du filtre (Excel et PDF), page après page.</summary>
    public static async Task<(List<PettyCashMovementDto> Rows, PettyCashPageDto First)> ValidatedAsync(IAppDbContext db, ICurrentUser caller, IClock clock, PettyCashFilter filter, int max,
        CancellationToken cancellationToken)
    {
        var validated = filter with { Status = ["validated"] };
        var first = await ListAsync(db, caller, clock, validated with { Paging = validated.Paging with { Page = 1, PageSize = 200 } }, cancellationToken);
        if (first.Total > max)
        {
            throw BusinessException.Validation(
                max == PdfMaxRows ? $"Le PDF est limité à {PdfMaxRows.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("fr-MA"))} mouvements ; affinez les filtres ou exportez en Excel."
                    : "Export trop volumineux.", new { limit = max, total = first.Total });
        }
        var rows = new List<PettyCashMovementDto>(first.Items);
        for (var page = 2; page <= (first.Total + 199) / 200; page++)
        {
            var next = await ListAsync(db, caller, clock, validated with { Paging = validated.Paging with { Page = page, PageSize = 200 } }, cancellationToken);
            rows.AddRange(next.Items);
            if (next.Items.Count == 0) break;
        }
        return (rows, first);
    }

    public static int PdfLimit => PdfMaxRows;

    public static async Task AssertAgencyReachableAsync(IAppDbContext db, ICurrentUser caller, string agencyId, CancellationToken cancellationToken)
    {
        if (!await db.Agencies.AnyAsync(a => a.Id == agencyId, cancellationToken)) throw BusinessException.Validation("Agence introuvable.", new { field = "agencyId" });
        if (caller.IsSuperAdmin()) return;
        if (caller.AgencyId != agencyId) throw BusinessException.Forbidden("Cette agence n'est pas dans votre périmètre.");
    }

    public static async Task AttachProofAsync(IAppDbContext db, IFileStorage storage, string id, ProofFileDto proof, string folder, CancellationToken cancellationToken)
    {
        var stored = await storage.PutAsync(folder, Convert.FromBase64String(proof.Content!), proof.MimeType!, cancellationToken);
        try
        {
            await db.PettyCashMovements.Where(m => m.Id == id).ExecuteUpdateAsync(set => set.SetProperty(m => m.ProofName, proof.Name).SetProperty(m => m.ProofMimeType, proof.MimeType)
                .SetProperty(m => m.ProofPath, stored.Key), cancellationToken);
        }
        catch
        {
            await storage.RemoveAsync(stored.Key, cancellationToken);
            throw;
        }
    }
}
