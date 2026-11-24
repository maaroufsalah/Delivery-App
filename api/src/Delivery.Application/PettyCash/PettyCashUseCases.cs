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

public sealed record PettyCashMovementDto(string Id, string Kind, string Date, long Amount, string AgencyId, string AgencyName, string? Label, string? Category, string? BuyerId,
    string? BuyerName, string? Note, string? ProofName, string? CreatedByName, DateTime CreatedAt, DateTime UpdatedAt, string Status, string? RefusalReason, string? DecidedByName,
    DateTime? DecidedAt, string DayStatus);

public sealed record PettyCashTotalsDto(long Balance, long Supplied, long Spent, int Drafts);

public sealed record PettyCashPageDto(IReadOnlyList<PettyCashMovementDto> Items, int Total, int Page, int PageSize, PettyCashTotalsDto Totals, string Period, string From, string To);

public sealed record PettyCashPersonDto(string Id, string Name);

public sealed record PettyCashAgencyDto(string Id, string Name);

public sealed record PettyCashMetaDto(IReadOnlyList<string> Categories, IReadOnlyList<PettyCashPersonDto> Buyers, IReadOnlyList<PettyCashAgencyDto> Agencies);

public sealed record PettyCashCourierPageDto(IReadOnlyList<PettyCashMovementDto> Items, int Total, int Page, int PageSize, int Drafts, IReadOnlyList<string> Categories);

/// <summary>`pettyCashListFilterSchema` (catégorie, acheteur et statut multiples).</summary>
public sealed record PettyCashFilter(ListQuery Paging, string Period, string? From, string? To, string? AgencyId, string? Kind, IReadOnlyList<string>? Category, IReadOnlyList<string>? BuyerId,
    IReadOnlyList<string>? Status);

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

/// <summary>`pettyCashProofSchema` : un justificatif joint (PDF ou image), en base64.</summary>
public sealed record ProofFileDto
{
    public string? Name { get; init => field = Zod.Trim(value); }
    public string? MimeType { get; init; }
    public string? Content { get; init; }
}

internal static class ProofRules
{
    public static readonly string[] Types = ["application/pdf", "image/jpeg", "image/png", "image/webp"];

    public static void Proof<T>(ZodValidator<T> validator, System.Linq.Expressions.Expression<Func<T, ProofFileDto?>> proof)
    {
        var get = proof.Compile();
        validator.RuleFor(x => get(x)!.Name).Required().MinLength(1, "required").MaxLength(120).OverridePropertyName("proof.name").When(x => get(x) is not null);
        validator.RuleFor(x => get(x)!.MimeType).Must(v => v is not null && Types.Contains(v)).WithMessage("unsupported-file-type").OverridePropertyName("proof.mimeType").When(x => get(x) is not null);
        validator.RuleFor(x => get(x)!.Content).Required().MinLength(1, "required").OverridePropertyName("proof.content").When(x => get(x) is not null);
    }

    /// <summary>`madAmount` (petite caisse) : requis, strictement positif, 1 000 000 MAD au plus.</summary>
    public static void Amount<T>(ZodValidator<T> validator, System.Linq.Expressions.Expression<Func<T, double?>> amount)
    {
        validator.RuleFor(amount).Must(v => v is not null).WithMessage("required").Must(v => v is null || v.Value > 0).WithMessage("must-be-positive")
            .Must(v => v is null || v.Value <= 1_000_000).WithMessage("amount-too-large");
    }
}

/// <summary>`GET /petty-cash`.</summary>
public sealed record GetPettyCashQuery(PettyCashFilter Filter) : IRequest<PettyCashPageDto>;

internal sealed class GetPettyCashHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<GetPettyCashQuery, PettyCashPageDto>
{
    public Task<PettyCashPageDto> Handle(GetPettyCashQuery request, CancellationToken cancellationToken) => PettyCashMovements.ListAsync(db, caller, clock, request.Filter, cancellationToken);
}

/// <summary>`GET /petty-cash/meta` : catégories, acheteurs possibles et agences.</summary>
public sealed record GetPettyCashMetaQuery : IRequest<PettyCashMetaDto>;

internal sealed class GetPettyCashMetaHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetPettyCashMetaQuery, PettyCashMetaDto>
{
    public async Task<PettyCashMetaDto> Handle(GetPettyCashMetaQuery request, CancellationToken cancellationToken)
    {
        var buyers = await PettyCashMovements.Buyers(db, caller).OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Select(u => new PettyCashPersonDto(u.Id, u.FirstName + " " + u.LastName)).ToListAsync(cancellationToken);
        var agencies = db.Agencies.AsNoTracking();
        if (!caller.IsSuperAdmin()) { var own = caller.AgencyId ?? Scope.DenyAllId; agencies = agencies.Where(a => a.Id == own); }
        return new PettyCashMetaDto(await PettyCashMovements.CategoriesAsync(db, cancellationToken), buyers,
            await agencies.OrderBy(a => a.Name).Select(a => new PettyCashAgencyDto(a.Id, a.Name)).ToListAsync(cancellationToken));
    }
}

/// <summary>`pettyCashCategoriesSchema` : la liste des catégories, sans doublon (casse ignorée).</summary>
public sealed record SetPettyCashCategoriesCommand : IRequest<PettyCashMetaDto>
{
    public IReadOnlyList<string>? Categories { get; init; }
}

public sealed class SetPettyCashCategoriesValidator : ZodValidator<SetPettyCashCategoriesCommand>
{
    public SetPettyCashCategoriesValidator()
    {
        RuleFor(x => x.Categories).Required("array").Custom((list, context) =>
        {
            if (list is null) return;
            if (list.Count < 1) context.AddFailure(ValidationPaths.Raw + "categories", "required");
            if (list.Count > 30) context.AddFailure(ValidationPaths.Raw + "categories", "Too big: expected array to have <=30 items");
            for (var i = 0; i < list.Count; i++)
            {
                var item = list[i]?.Trim() ?? string.Empty;
                if (item.Length < 1) context.AddFailure(ValidationPaths.Raw + $"categories.{i}", "required");
                else if (item.Length > 60) context.AddFailure(ValidationPaths.Raw + $"categories.{i}", Zod.TooBigString(60));
            }
            if (list.Select(c => (c ?? string.Empty).Trim().ToLowerInvariant()).Distinct().Count() != list.Count) context.AddFailure(ValidationPaths.Raw + "categories", "invalid");
        });
    }
}

internal sealed class SetPettyCashCategoriesHandler(IAppDbContext db, ICurrentUser caller, ISender sender) : IRequestHandler<SetPettyCashCategoriesCommand, PettyCashMetaDto>
{
    public async Task<PettyCashMetaDto> Handle(SetPettyCashCategoriesCommand input, CancellationToken cancellationToken)
    {
        var company = await db.CompanyAsync(cancellationToken);
        await db.SetSettingAsync(company.Id, PettyCashMovements.CategoriesSetting, new JsonArray(input.Categories!.Select(c => (JsonNode?)JsonValue.Create(c.Trim())).ToArray()), cancellationToken);
        _ = caller;
        return await sender.Send(new GetPettyCashMetaQuery(), cancellationToken);
    }
}

/// <summary>`pettyCashMovementSchema` : un approvisionnement ou une dépense saisis par l'agence.</summary>
public sealed record PettyCashMovementCommand : IRequest<PettyCashMovementDto>
{
    [JsonIgnore]
    public string? Id { get; init; }

    public string? Kind { get; init; }
    public string? Date { get; init; }
    public double? Amount { get; init; }
    public string? AgencyId { get; init; }
    public string? Label { get; init => field = Zod.Trim(value); }
    public string? Category { get; init => field = Zod.Trim(value); }
    public string? BuyerId { get; init => field = Zod.Trim(value); }
    public string? Note { get; init => field = Zod.Trim(value); }
    public ProofFileDto? Proof { get; init; }
    public bool? RemoveProof { get; init; }
}

public sealed class PettyCashMovementValidator : ZodValidator<PettyCashMovementCommand>
{
    public PettyCashMovementValidator()
    {
        RuleFor(x => x.Kind).Required().OneOf(PettyCashMovements.Kinds);
        RuleFor(x => x.Date).Required().Matches(@"^\d{4}-\d{2}-\d{2}$").WithMessage("invalid-date");
        ProofRules.Amount(this, x => x.Amount);
        RuleFor(x => x.AgencyId).Required().MinLength(1, "required");
        RuleFor(x => x.Label).MaxLength(160);
        RuleFor(x => x.Category).MaxLength(60);
        RuleFor(x => x.BuyerId).MaxLength(40);
        RuleFor(x => x.Note).MaxLength(500);
        ProofRules.Proof(this, x => x.Proof);
    }

    protected override void Refine(PettyCashMovementCommand value, Action<string, string> issue)
    {
        if (value.Kind != "expense") return;
        if (string.IsNullOrEmpty(value.Label)) issue(ValidationPaths.Raw + "label", "required");
        if (string.IsNullOrEmpty(value.Category)) issue(ValidationPaths.Raw + "category", "required");
        if (string.IsNullOrEmpty(value.BuyerId)) issue(ValidationPaths.Raw + "buyerId", "required");
    }
}

internal sealed class PettyCashMovementHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IFileStorage storage) : IRequestHandler<PettyCashMovementCommand, PettyCashMovementDto>
{
    public async Task<PettyCashMovementDto> Handle(PettyCashMovementCommand input, CancellationToken cancellationToken)
    {
        var scoped = PettyCashMovements.Scoped(db.PettyCashMovements.AsNoTracking(), caller);
        var current = input.Id is null ? null
            : await scoped.Where(m => m.Id == input.Id).Select(m => new { m.Id, m.AgencyId, m.OccurredAt, m.Category, m.BuyerId }).FirstOrDefaultAsync(cancellationToken)
              ?? throw BusinessException.NotFound("Mouvement de petite caisse introuvable.");
        await PettyCashMovements.AssertAgencyReachableAsync(db, caller, input.AgencyId!, cancellationToken);
        var expense = input.Kind == "expense";
        string? buyerName = null;
        if (expense)
        {
            var categories = await PettyCashMovements.CategoriesAsync(db, cancellationToken);
            if (!categories.Contains(input.Category ?? string.Empty) && current?.Category != input.Category) throw BusinessException.Validation("Catégorie inconnue.", new { field = "category" });
            var keepsBuyer = current?.BuyerId is not null && current.BuyerId == input.BuyerId;
            var buyers = keepsBuyer ? db.Users.AsNoTracking() : PettyCashMovements.Buyers(db, caller);
            var buyerId = (keepsBuyer ? current!.BuyerId : input.BuyerId) ?? Scope.DenyAllId;
            var buyer = await buyers.Where(u => u.Id == buyerId).Select(u => new { u.FirstName, u.LastName }).FirstOrDefaultAsync(cancellationToken)
                        ?? throw BusinessException.Validation("Ce membre de l’équipe est introuvable.", new { field = "buyerId" });
            buyerName = $"{buyer.FirstName} {buyer.LastName}";
        }
        var occurredAt = DateTime.SpecifyKind(DateTime.ParseExact($"{input.Date}T12:00:00", "yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Utc);
        var amount = Money.FromCents((long)Math.Round(input.Amount!.Value * 100, MidpointRounding.AwayFromZero));
        var targets = new List<(string, string)> { (input.AgencyId!, input.Date!) };
        if (current is not null) targets.Insert(0, (current.AgencyId, PettyCashDays.DayOf(current.OccurredAt)));
        var id = await PettyCashDays.WriteAsync(db, clock, targets, async () =>
        {
            var row = current is null ? new PettyCashMovement { CreatedById = caller.UserId, CreatedAt = DateTime.UtcNow } : await db.PettyCashMovements.FirstAsync(m => m.Id == current.Id, cancellationToken);
            row.Kind = expense ? PettyCashKind.EXPENSE : PettyCashKind.SUPPLY;
            row.OccurredAt = occurredAt;
            row.Amount = amount;
            row.AgencyId = input.AgencyId!;
            row.Label = expense ? ReturnLabel(input.Label) : null;
            row.Category = expense ? ReturnLabel(input.Category) : null;
            row.BuyerId = expense ? input.BuyerId : null;
            row.BuyerName = buyerName;
            row.Note = string.IsNullOrEmpty(input.Note) ? null : input.Note;
            row.UpdatedAt = DateTime.UtcNow;
            if (current is not null && input.RemoveProof == true && input.Proof is null) { row.ProofName = null; row.ProofMimeType = null; row.ProofPath = null; }
            if (current is null) db.PettyCashMovements.Add(row);
            await db.SaveChangesAsync(cancellationToken);
            return row.Id;
        }, cancellationToken);
        db.ChangeTracker.Clear();
        if (input.Proof is not null) await PettyCashMovements.AttachProofAsync(db, storage, id, input.Proof, $"petty-cash/{id}", cancellationToken);
        return await PettyCashMovements.OneAsync(db, PettyCashMovements.Scoped(db.PettyCashMovements.AsNoTracking(), caller), id, "Mouvement de petite caisse introuvable.", cancellationToken);
    }

    private static string? ReturnLabel(string? value) => string.IsNullOrEmpty(value) ? null : value;
}

/// <summary>`DELETE /petty-cash/:id` (204).</summary>
public sealed record DeletePettyCashCommand(string Id) : IRequest<Unit>;

internal sealed class DeletePettyCashHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<DeletePettyCashCommand, Unit>
{
    public async Task<Unit> Handle(DeletePettyCashCommand request, CancellationToken cancellationToken)
    {
        var current = await PettyCashMovements.Scoped(db.PettyCashMovements.AsNoTracking(), caller).Where(m => m.Id == request.Id).Select(m => new { m.Id, m.AgencyId, m.OccurredAt })
                          .FirstOrDefaultAsync(cancellationToken) ?? throw BusinessException.NotFound("Mouvement de petite caisse introuvable.");
        await PettyCashDays.WriteAsync(db, clock, [(current.AgencyId, PettyCashDays.DayOf(current.OccurredAt))],
            async () => await db.PettyCashMovements.Where(m => m.Id == current.Id).ExecuteDeleteAsync(cancellationToken), cancellationToken);
        return Unit.Value;
    }
}

/// <summary>`PATCH /petty-cash/:id/validate|refuse` : une dépense de livreur en brouillon est tranchée.</summary>
public sealed record DecidePettyCashCommand : IRequest<PettyCashMovementDto>
{
    [JsonIgnore]
    public string Id { get; init; } = string.Empty;

    [JsonIgnore]
    public bool Validate { get; init; }

    public string? Reason { get; init => field = Zod.Trim(value); }
}

public sealed class DecidePettyCashValidator : ZodValidator<DecidePettyCashCommand>
{
    public DecidePettyCashValidator()
    {
        RuleFor(x => x.Reason).MaxLength(300);
    }
}

internal sealed class DecidePettyCashHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<DecidePettyCashCommand, PettyCashMovementDto>
{
    public async Task<PettyCashMovementDto> Handle(DecidePettyCashCommand input, CancellationToken cancellationToken)
    {
        var scoped = PettyCashMovements.Scoped(db.PettyCashMovements.AsNoTracking(), caller);
        var current = await scoped.Where(m => m.Id == input.Id).Select(m => new { m.Id, m.AgencyId, m.OccurredAt, m.Status }).FirstOrDefaultAsync(cancellationToken)
                      ?? throw BusinessException.NotFound("Mouvement de petite caisse introuvable.");
        if (current.Status != PettyCashStatus.DRAFT) throw BusinessException.Conflict("Cette dépense a déjà été traitée.", new { reason = "petty-cash-not-draft" });
        var status = input.Validate ? PettyCashStatus.VALIDATED : PettyCashStatus.REFUSED;
        var reason = input.Validate || string.IsNullOrEmpty(input.Reason) ? null : input.Reason;
        var userId = caller.UserId;
        await PettyCashDays.WriteAsync(db, clock, [(current.AgencyId, PettyCashDays.DayOf(current.OccurredAt))], async () =>
            await db.PettyCashMovements.Where(m => m.Id == current.Id).ExecuteUpdateAsync(set => set.SetProperty(m => m.Status, status).SetProperty(m => m.RefusalReason, reason)
                .SetProperty(m => m.DecidedById, userId).SetProperty(m => m.DecidedAt, DateTime.UtcNow).SetProperty(m => m.UpdatedAt, DateTime.UtcNow), cancellationToken), cancellationToken);
        return await PettyCashMovements.OneAsync(db, scoped, input.Id, "Mouvement de petite caisse introuvable.", cancellationToken);
    }
}

/// <summary>`GET /petty-cash/:id/proof` : le justificatif tel qu'il a été déposé.</summary>
public sealed record GetPettyCashProofQuery(string Id) : IRequest<FileContent>;

internal sealed class GetPettyCashProofHandler(IAppDbContext db, ICurrentUser caller, IFileStorage storage) : IRequestHandler<GetPettyCashProofQuery, FileContent>
{
    public async Task<FileContent> Handle(GetPettyCashProofQuery request, CancellationToken cancellationToken)
    {
        var row = await PettyCashMovements.Scoped(db.PettyCashMovements.AsNoTracking(), caller).Where(m => m.Id == request.Id).Select(m => new { m.ProofPath, m.ProofName, m.ProofMimeType })
                      .FirstOrDefaultAsync(cancellationToken) ?? throw BusinessException.NotFound("Mouvement de petite caisse introuvable.");
        if (row.ProofPath is null) throw BusinessException.NotFound("Aucun justificatif sur ce mouvement.", new { reason = "no-proof" });
        var content = await storage.GetAsync(row.ProofPath, cancellationToken);
        return new FileContent(content, row.ProofMimeType ?? "application/octet-stream", Uri.EscapeDataString(row.ProofName ?? "justificatif"), CacheControl: "private, no-store");
    }
}

internal static class PettyCashExports
{
    public static readonly IReadOnlyList<ExportColumn<PettyCashMovementDto>> Columns =
    [
        new("Date", r => Exports.CalendarDateText(PettyCashDays.Parse(r.Date))),
        new("Type", r => r.Kind == "supply" ? "Approvisionnement" : "Dépense"),
        new("Libellé", r => r.Label ?? string.Empty),
        new("Catégorie", r => r.Category ?? string.Empty),
        new("Montant", r => Exports.Amount(r.Kind == "expense" ? -r.Amount : r.Amount)),
        new("Acheté par", r => r.BuyerName ?? string.Empty),
        new("Agence", r => r.AgencyName),
        new("Note", r => r.Note ?? string.Empty),
        new("Justificatif", r => r.ProofName ?? string.Empty),
        new("Saisi par", r => r.CreatedByName ?? string.Empty),
        new("Saisi le", r => Exports.DateTimeText(r.CreatedAt)),
    ];

    /// <summary>`dayQuery` : la journée seule, mouvements validés, dans l'ordre de saisie.</summary>
    public static PettyCashFilter Day(string day, string? agencyId) =>
        new(new ListQuery { Page = 1, PageSize = 25, Order = "asc" }, "custom", day, day, agencyId, null, null, null, ["validated"]);

    public static async Task<FileContent> PdfAsync(IAppDbContext db, ICurrentUser caller, IClock clock, IPdfService pdf, IFileStorage storage, PettyCashFilter filter, PettyCashDaySummaryDto? day,
        CancellationToken cancellationToken)
    {
        var (rows, first) = await PettyCashMovements.ValidatedAsync(db, caller, clock, filter, PettyCashMovements.PdfLimit, cancellationToken);
        var company = await PdfCompany.LoadAsync(db, "Petite caisse", cancellationToken);
        var identity = await db.Companies.AsNoTracking().OrderBy(c => c.CreatedAt).Select(c => new { c.Id, c.LogoUrl }).FirstOrDefaultAsync(cancellationToken);
        byte[]? logo = null;
        if (identity is not null && Settings.Logos.KeyOf(identity.Id, identity.LogoUrl) is { } key && !key.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
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
        var generatedAt = clock.UtcNow;
        var sheet = new PettyCashSheet(company, logo, generatedAt, first.From, first.To, first.Totals.Balance, first.Totals.Supplied, first.Totals.Spent,
            day is null ? null : new PettyCashSheetDay(day.Status, day.OpeningBalance, day.Supplied, day.Spent, day.ClosingBalance, day.ClosedAt, day.ClosedBy, day.PendingDrafts, day.ReopenReason),
            rows.Select(r => new PettyCashSheetLine(r.Date, r.Kind, r.Label, r.Note, r.Category, r.BuyerName, r.Amount)).ToList());
        var fileName = day is null ? $"Petite-Caisse-{generatedAt:yyyy-MM-dd}.pdf" : $"Petite-Caisse-Journee-{day.Day}.pdf";
        return new FileContent(pdf.Render(sheet), FileContent.Pdf, fileName, CacheControl: "private, no-store");
    }
}

/// <summary>`GET /petty-cash/export` : les mouvements validés du filtre, classeur « Petite caisse ».</summary>
public sealed record ExportPettyCashQuery(PettyCashFilter Filter) : IRequest<FileContent>;

internal sealed class ExportPettyCashHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IExcelService excel) : IRequestHandler<ExportPettyCashQuery, FileContent>
{
    public async Task<FileContent> Handle(ExportPettyCashQuery request, CancellationToken cancellationToken)
    {
        var (rows, _) = await PettyCashMovements.ValidatedAsync(db, caller, clock, request.Filter, Exports.MaxRows, cancellationToken);
        return await Exports.ToXlsxAsync(db, excel, clock, caller, "Petite caisse", PettyCashExports.Columns, rows, cancellationToken);
    }
}

/// <summary>`GET /petty-cash/pdf`.</summary>
public sealed record GetPettyCashPdfQuery(PettyCashFilter Filter) : IRequest<FileContent>;

internal sealed class GetPettyCashPdfHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IPdfService pdf, IFileStorage storage) : IRequestHandler<GetPettyCashPdfQuery, FileContent>
{
    public Task<FileContent> Handle(GetPettyCashPdfQuery request, CancellationToken cancellationToken) =>
        PettyCashExports.PdfAsync(db, caller, clock, pdf, storage, request.Filter, null, cancellationToken);
}

/// <summary>`pettyCashDaysQuerySchema`.</summary>
public sealed record GetPettyCashDaysQuery(string? AgencyId, string? Status, string? WithDrafts, string? Limit) : IRequest<PettyCashDaysPageDto>;

internal sealed class GetPettyCashDaysHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<GetPettyCashDaysQuery, PettyCashDaysPageDto>
{
    public Task<PettyCashDaysPageDto> Handle(GetPettyCashDaysQuery request, CancellationToken cancellationToken)
    {
        var issues = new List<object>();
        if (request.Status is { } s && s is not ("open" or "closed")) issues.Add(new { path = "status", message = Zod.InvalidOption(["open", "closed"]) });
        if (request.WithDrafts is { } w && w is not ("true" or "false")) issues.Add(new { path = "withDrafts", message = Zod.InvalidOption(["true", "false"]) });
        var limit = 400;
        if (request.Limit is { } raw)
        {
            if (!int.TryParse(raw.Trim(), out limit) || limit < 1 || limit > 1000) issues.Add(new { path = "limit", message = "Invalid input" });
        }
        if (issues.Count > 0) throw BusinessException.Validation("Saisie invalide.", new { issues });
        return PettyCashDays.ListAsync(db, caller, clock, request.AgencyId, request.Status, request.WithDrafts == "true", limit, cancellationToken);
    }
}

internal static class DayParam
{
    public static void Check(string day)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(day, @"^\d{4}-\d{2}-\d{2}$")) throw BusinessException.Validation("Saisie invalide.", new { issues = new[] { new { path = "", message = "invalid-date" } } });
    }
}

/// <summary>`GET /petty-cash/days/:day`.</summary>
public sealed record GetPettyCashDayQuery(string Day, string? AgencyId) : IRequest<PettyCashDayDetailDto>;

internal sealed class GetPettyCashDayHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetPettyCashDayQuery, PettyCashDayDetailDto>
{
    public Task<PettyCashDayDetailDto> Handle(GetPettyCashDayQuery request, CancellationToken cancellationToken)
    {
        DayParam.Check(request.Day);
        return PettyCashDays.DetailAsync(db, caller, request.Day, request.AgencyId, cancellationToken);
    }
}

/// <summary>`POST /petty-cash/days/:day/close|reopen` (`pettyCashDayCloseSchema`, `pettyCashDayReopenSchema`).</summary>
public sealed record PettyCashDayActionCommand : IRequest<PettyCashDayDetailDto>
{
    [JsonIgnore]
    public string Day { get; init; } = string.Empty;

    [JsonIgnore]
    public bool Reopen { get; init; }

    public string? AgencyId { get; init => field = Zod.Trim(value); }
    public string? Reason { get; init => field = Zod.Trim(value); }
}

public sealed class PettyCashDayActionValidator : ZodValidator<PettyCashDayActionCommand>
{
    public PettyCashDayActionValidator()
    {
        RuleFor(x => x.Reason).Required().MinLength(1, "required").MaxLength(300).When(x => x.Reopen);
    }
}

internal sealed class PettyCashDayActionHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<PettyCashDayActionCommand, PettyCashDayDetailDto>
{
    public Task<PettyCashDayDetailDto> Handle(PettyCashDayActionCommand input, CancellationToken cancellationToken)
    {
        DayParam.Check(input.Day);
        var agencyId = string.IsNullOrEmpty(input.AgencyId) ? null : input.AgencyId;
        return input.Reopen
            ? PettyCashDays.ReopenAsync(db, caller, input.Day, agencyId, input.Reason!, cancellationToken)
            : PettyCashDays.CloseAsync(db, caller, input.Day, agencyId, cancellationToken);
    }
}

/// <summary>`GET /petty-cash/days/:day/export` : la journée en classeur.</summary>
public sealed record ExportPettyCashDayQuery(string Day, string? AgencyId) : IRequest<FileContent>;

internal sealed class ExportPettyCashDayHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IExcelService excel) : IRequestHandler<ExportPettyCashDayQuery, FileContent>
{
    public async Task<FileContent> Handle(ExportPettyCashDayQuery request, CancellationToken cancellationToken)
    {
        DayParam.Check(request.Day);
        await PettyCashDays.DetailAsync(db, caller, request.Day, request.AgencyId, cancellationToken);
        var (rows, _) = await PettyCashMovements.ValidatedAsync(db, caller, clock, PettyCashExports.Day(request.Day, request.AgencyId), Exports.MaxRows, cancellationToken);
        return await Exports.ToXlsxAsync(db, excel, clock, caller, $"Petite caisse {request.Day}", PettyCashExports.Columns, rows, cancellationToken);
    }
}

/// <summary>`GET /petty-cash/days/:day/pdf` : la journée, avec son bandeau d'ouverture et de clôture.</summary>
public sealed record GetPettyCashDayPdfQuery(string Day, string? AgencyId) : IRequest<FileContent>;

internal sealed class GetPettyCashDayPdfHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IPdfService pdf, IFileStorage storage) : IRequestHandler<GetPettyCashDayPdfQuery, FileContent>
{
    public async Task<FileContent> Handle(GetPettyCashDayPdfQuery request, CancellationToken cancellationToken)
    {
        DayParam.Check(request.Day);
        var detail = await PettyCashDays.DetailAsync(db, caller, request.Day, request.AgencyId, cancellationToken);
        return await PettyCashExports.PdfAsync(db, caller, clock, pdf, storage, PettyCashExports.Day(request.Day, request.AgencyId), detail.Summary, cancellationToken);
    }
}

/// <summary>`PettyCashCourierService` : les dépenses du livreur, brouillons jusqu'à la décision de l'agence.</summary>
internal static class CourierExpenses
{
    public static IQueryable<PettyCashMovement> Mine(IAppDbContext db, ICurrentUser caller)
    {
        if (caller.CourierId is null || caller.UserId is null) return db.PettyCashMovements.AsNoTracking().Where(m => m.Id == Scope.DenyAllId);
        var userId = caller.UserId;
        return db.PettyCashMovements.AsNoTracking().Where(m => m.Kind == PettyCashKind.EXPENSE && m.CreatedById == userId && m.BuyerId == userId);
    }

    public static BusinessException NotDraft() => BusinessException.Forbidden("Cette dépense a déjà été traitée : elle ne se modifie plus.", new { reason = "petty-cash-not-draft" });

    public static async Task<(string Id, string AgencyId, DateTime OccurredAt, string? Category)> DraftAsync(IAppDbContext db, ICurrentUser caller, string id, CancellationToken cancellationToken)
    {
        var row = await Mine(db, caller).Where(m => m.Id == id).Select(m => new { m.Id, m.AgencyId, m.OccurredAt, m.Category, m.Status }).FirstOrDefaultAsync(cancellationToken)
                  ?? throw BusinessException.NotFound("Dépense introuvable.");
        if (row.Status != PettyCashStatus.DRAFT) throw NotDraft();
        return (row.Id, row.AgencyId, row.OccurredAt, row.Category);
    }

    public static async Task AssertStillDraftAsync(IAppDbContext db, string id, CancellationToken cancellationToken)
    {
        if (!await db.PettyCashMovements.AnyAsync(m => m.Id == id && m.Status == PettyCashStatus.DRAFT, cancellationToken)) throw NotDraft();
    }

    public static async Task AssertCategoryAsync(IAppDbContext db, string category, string? current, CancellationToken cancellationToken)
    {
        if (!(await PettyCashMovements.CategoriesAsync(db, cancellationToken)).Contains(category) && current != category) throw BusinessException.Validation("Catégorie inconnue.", new { field = "category" });
    }

    public static async Task<StoredFile?> StoreAsync(IFileStorage storage, ProofFileDto? proof, CancellationToken cancellationToken) =>
        proof is null ? null : await storage.PutAsync("petty-cash/courier", Convert.FromBase64String(proof.Content!), proof.MimeType!, cancellationToken);
}

/// <summary>`GET /courier/me/petty-cash` (`pettyCashCourierListQuerySchema`).</summary>
public sealed record GetMyPettyCashQuery(string? Page, string? PageSize) : IRequest<PettyCashCourierPageDto>;

internal sealed class GetMyPettyCashHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetMyPettyCashQuery, PettyCashCourierPageDto>
{
    public async Task<PettyCashCourierPageDto> Handle(GetMyPettyCashQuery request, CancellationToken cancellationToken)
    {
        var paging = new ListQueryParams { Page = request.Page, PageSize = request.PageSize };
        var query = paging.ToQueryWithDefault(50);
        if (query.PageSize > 100) throw BusinessException.Validation("Saisie invalide.", new { issues = new[] { new { path = "pageSize", message = "Too big: expected number to be <=100" } } });
        var where = CourierExpenses.Mine(db, caller);
        var total = await where.CountAsync(cancellationToken);
        var items = await PettyCashMovements.ProjectAsync(db, where.OrderByDescending(m => m.OccurredAt).ThenByDescending(m => m.CreatedAt).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize),
            cancellationToken);
        var drafts = await where.CountAsync(m => m.Status == PettyCashStatus.DRAFT, cancellationToken);
        return new PettyCashCourierPageDto(items, total, query.Page, query.PageSize, drafts, await PettyCashMovements.CategoriesAsync(db, cancellationToken));
    }
}

/// <summary>`GET /courier/me/petty-cash/export` : classeur « Mes dépenses ».</summary>
public sealed record ExportMyPettyCashQuery : IRequest<FileContent>;

internal sealed class ExportMyPettyCashHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IExcelService excel) : IRequestHandler<ExportMyPettyCashQuery, FileContent>
{
    private static readonly Dictionary<string, string> Labels = new() { ["draft"] = "Brouillon", ["validated"] = "Validée", ["refused"] = "Refusée" };

    private static readonly IReadOnlyList<ExportColumn<PettyCashMovementDto>> Columns =
    [
        new("Date", r => Exports.CalendarDateText(PettyCashDays.Parse(r.Date))),
        new("Libellé", r => r.Label ?? string.Empty),
        new("Catégorie", r => r.Category ?? string.Empty),
        new("Montant", r => Exports.Amount(r.Amount)),
        new("Statut", r => Labels[r.Status]),
        new("Motif du refus", r => r.RefusalReason ?? string.Empty),
        new("Justificatif", r => r.ProofName ?? string.Empty),
        new("Saisi le", r => Exports.DateTimeText(r.CreatedAt)),
        new("Traité le", r => Exports.DateTimeText(r.DecidedAt)),
    ];

    public async Task<FileContent> Handle(ExportMyPettyCashQuery request, CancellationToken cancellationToken)
    {
        var where = CourierExpenses.Mine(db, caller);
        Exports.AssertWithinLimit(await where.CountAsync(cancellationToken));
        var rows = await PettyCashMovements.ProjectAsync(db, where.OrderByDescending(m => m.OccurredAt).ThenByDescending(m => m.CreatedAt), cancellationToken);
        return await Exports.ToXlsxAsync(db, excel, clock, caller, "Mes dépenses", Columns, rows, cancellationToken);
    }
}

/// <summary>`pettyCashCourierExpenseSchema` : la dépense du livreur, sur la journée de son agence.</summary>
public sealed record MyPettyCashExpenseCommand : IRequest<object?>
{
    [JsonIgnore]
    public string? Id { get; init; }

    [JsonIgnore]
    public string? IdempotencyKey { get; init; }

    public string? Label { get; init => field = Zod.Trim(value); }
    public string? Category { get; init => field = Zod.Trim(value); }
    public double? Amount { get; init; }
    public string? Note { get; init => field = Zod.Trim(value); }
    public ProofFileDto? Proof { get; init; }
    public bool? RemoveProof { get; init; }
}

public sealed class MyPettyCashExpenseValidator : ZodValidator<MyPettyCashExpenseCommand>
{
    public MyPettyCashExpenseValidator()
    {
        RuleFor(x => x.Label).Required().MinLength(1, "required").MaxLength(160);
        RuleFor(x => x.Category).Required().MinLength(1, "required").MaxLength(60);
        ProofRules.Amount(this, x => x.Amount);
        RuleFor(x => x.Note).MaxLength(500);
        ProofRules.Proof(this, x => x.Proof);
    }
}

internal sealed class MyPettyCashExpenseHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IFileStorage storage) : IRequestHandler<MyPettyCashExpenseCommand, object?>
{
    public async Task<object?> Handle(MyPettyCashExpenseCommand input, CancellationToken cancellationToken) => input.Id is null
        ? await IdempotentReplay.RunAsync(db, caller, input.IdempotencyKey, "POST /courier/me/petty-cash", () => CreateAsync(input, cancellationToken), cancellationToken)
        : await UpdateAsync(input, cancellationToken);

    private async Task<PettyCashMovementDto> CreateAsync(MyPettyCashExpenseCommand input, CancellationToken cancellationToken)
    {
        var courier = await db.Couriers.AsNoTracking().Where(c => c.Id == caller.CourierId && c.DeletedAt == null).Select(c => new { c.AgencyId, c.CityId }).FirstOrDefaultAsync(cancellationToken)
                      ?? throw BusinessException.NotFound("Fiche livreur introuvable.");
        var agencyId = await db.AgencyForCourierAsync(courier.AgencyId, courier.CityId, cancellationToken)
                       ?? throw BusinessException.Validation("Aucune agence ne peut recevoir cette dépense.", new { reason = "no-agency" });
        var user = await db.Users.AsNoTracking().Where(u => u.Id == caller.UserId).Select(u => new { u.FirstName, u.LastName }).FirstOrDefaultAsync(cancellationToken);
        await CourierExpenses.AssertCategoryAsync(db, input.Category!, null, cancellationToken);
        var day = await PettyCashDays.CourierDayAsync(db, clock, agencyId, cancellationToken);
        var proof = await CourierExpenses.StoreAsync(storage, input.Proof, cancellationToken);
        string id;
        try
        {
            id = await PettyCashDays.WriteAsync(db, clock, [(agencyId, day)], async () =>
            {
                var row = new PettyCashMovement
                {
                    AgencyId = agencyId, Kind = PettyCashKind.EXPENSE, Status = PettyCashStatus.DRAFT,
                    OccurredAt = DateTime.SpecifyKind(PettyCashDays.Parse(day).ToDateTime(new TimeOnly(12, 0)), DateTimeKind.Utc),
                    Amount = Money.FromCents((long)Math.Round(input.Amount!.Value * 100, MidpointRounding.AwayFromZero)), Label = input.Label, Category = input.Category, BuyerId = caller.UserId,
                    BuyerName = user is null ? null : $"{user.FirstName} {user.LastName}", Note = string.IsNullOrEmpty(input.Note) ? null : input.Note, CreatedById = caller.UserId,
                    ProofName = proof is null ? null : input.Proof!.Name, ProofMimeType = proof is null ? null : input.Proof!.MimeType, ProofPath = proof?.Key, CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };
                db.PettyCashMovements.Add(row);
                await db.SaveChangesAsync(cancellationToken);
                return row.Id;
            }, cancellationToken);
        }
        catch
        {
            if (proof is not null) await storage.RemoveAsync(proof.Key, CancellationToken.None);
            throw;
        }
        db.ChangeTracker.Clear();
        return await PettyCashMovements.OneAsync(db, CourierExpenses.Mine(db, caller), id, "Dépense introuvable.", cancellationToken);
    }

    private async Task<PettyCashMovementDto> UpdateAsync(MyPettyCashExpenseCommand input, CancellationToken cancellationToken)
    {
        var current = await CourierExpenses.DraftAsync(db, caller, input.Id!, cancellationToken);
        await CourierExpenses.AssertCategoryAsync(db, input.Category!, current.Category, cancellationToken);
        var proof = await CourierExpenses.StoreAsync(storage, input.Proof, cancellationToken);
        try
        {
            await PettyCashDays.WriteAsync(db, clock, [(current.AgencyId, PettyCashDays.DayOf(current.OccurredAt))], async () =>
            {
                await CourierExpenses.AssertStillDraftAsync(db, current.Id, cancellationToken);
                var row = await db.PettyCashMovements.FirstAsync(m => m.Id == current.Id, cancellationToken);
                row.Label = input.Label;
                row.Category = input.Category;
                row.Amount = Money.FromCents((long)Math.Round(input.Amount!.Value * 100, MidpointRounding.AwayFromZero));
                row.Note = string.IsNullOrEmpty(input.Note) ? null : input.Note;
                row.UpdatedAt = DateTime.UtcNow;
                if (proof is not null) { row.ProofName = input.Proof!.Name; row.ProofMimeType = input.Proof.MimeType; row.ProofPath = proof.Key; }
                else if (input.RemoveProof == true) { row.ProofName = null; row.ProofMimeType = null; row.ProofPath = null; }
                await db.SaveChangesAsync(cancellationToken);
                return true;
            }, cancellationToken, draftOnly: true);
        }
        catch
        {
            if (proof is not null) await storage.RemoveAsync(proof.Key, CancellationToken.None);
            throw;
        }
        db.ChangeTracker.Clear();
        return await PettyCashMovements.OneAsync(db, CourierExpenses.Mine(db, caller), input.Id!, "Dépense introuvable.", cancellationToken);
    }
}

/// <summary>`DELETE /courier/me/petty-cash/:id` (204) : un brouillon seulement.</summary>
public sealed record DeleteMyPettyCashCommand(string Id) : IRequest<Unit>;

internal sealed class DeleteMyPettyCashHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<DeleteMyPettyCashCommand, Unit>
{
    public async Task<Unit> Handle(DeleteMyPettyCashCommand request, CancellationToken cancellationToken)
    {
        var current = await CourierExpenses.DraftAsync(db, caller, request.Id, cancellationToken);
        await PettyCashDays.WriteAsync(db, clock, [(current.AgencyId, PettyCashDays.DayOf(current.OccurredAt))], async () =>
        {
            await CourierExpenses.AssertStillDraftAsync(db, current.Id, cancellationToken);
            return await db.PettyCashMovements.Where(m => m.Id == current.Id).ExecuteDeleteAsync(cancellationToken);
        }, cancellationToken, draftOnly: true);
        return Unit.Value;
    }
}

/// <summary>`PettyCashDaysService.sweep` : la tâche de fond qui clôt les journées écoulées.</summary>
public sealed record SweepPettyCashDaysCommand : IRequest<PettyCashSweepDto>;

public sealed record PettyCashSweepDto(int Created, int Closed);

internal sealed class SweepPettyCashDaysHandler(IAppDbContext db, IClock clock) : IRequestHandler<SweepPettyCashDaysCommand, PettyCashSweepDto>
{
    public async Task<PettyCashSweepDto> Handle(SweepPettyCashDaysCommand request, CancellationToken cancellationToken)
    {
        var (created, closed) = await PettyCashDays.SweepAsync(db, clock, cancellationToken);
        return new PettyCashSweepDto(created, closed);
    }
}
