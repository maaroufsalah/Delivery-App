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

public class ChequeListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public long Amount { get; set; }
    public string? Bank { get; set; }
    public string? ChequeDate { get; set; }
    public DateTime ReceivedAt { get; set; }
    public string PayerName { get; set; } = string.Empty;
    public string ParcelId { get; set; } = string.Empty;
    public string ParcelCode { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string CourierId { get; set; } = string.Empty;
    public string CourierName { get; set; } = string.Empty;
    public string? InvoiceCode { get; set; }
}

public sealed record ChequeEventDto(string Id, string Status, string Kind, string? Comment, string AuthorName, DateTime CreatedAt);

public sealed class ChequeDetailDto : ChequeListItemDto
{
    [JsonPropertyOrder(1)] public string? Note { get; set; }
    [JsonPropertyOrder(1)] public string? RejectionReason { get; set; }
    [JsonPropertyOrder(1)] public List<ChequeEventDto> Events { get; set; } = [];
    [JsonPropertyOrder(1)] public string? NextStatus { get; set; }
    [JsonPropertyOrder(1)] public bool Rejectable { get; set; }
    [JsonPropertyOrder(1)] public bool Cancellable { get; set; }
}

/// <summary>`ChequePage` : la page, et la somme de tous les chèques filtrés.</summary>
public sealed record ChequePageDto(IReadOnlyList<ChequeListItemDto> Items, int Total, int Page, int PageSize, long TotalAmount);

public sealed record ChequeBulkSkipDto(string Id, string? Number, string Reason, string? Status);

public sealed record ChequeBulkResultDto(IReadOnlyList<string> Done, IReadOnlyList<ChequeBulkSkipDto> Skipped);

/// <summary>`ChequesService` : portée, parcours et écritures d'un chèque.</summary>
public static class Cheques
{
    public static readonly string[] Statuses = ["recu-livreur", "remis-admin", "recu-admin", "transmis-vendeur", "recu-vendeur", "rejete", "annule"];
    private static readonly string[] Flow = ["recu-livreur", "remis-admin", "recu-admin", "transmis-vendeur", "recu-vendeur"];
    public const string PendingFilter = "en-cours";

    public static string? Next(string status) => Array.IndexOf(Flow, status) is var at and >= 0 && at + 1 < Flow.Length ? Flow[at + 1] : null;

    public static bool CanReject(string status) => status is "recu-admin" or "transmis-vendeur" or "recu-vendeur";

    public static bool CanCancel(string status) => status is not ("recu-vendeur" or "annule" or "rejete");

    /// <summary>`scoped` : administrateur tout, livreur les siens, vendeur ceux de ses colis, agent ceux de son agence.</summary>
    public static IQueryable<Cheque> Scoped(IQueryable<Cheque> cheques, ICurrentUser caller)
    {
        if (caller.IsSuperAdmin()) return cheques;
        if (caller.CourierId is { } courierId) return cheques.Where(c => c.CourierId == courierId);
        if (caller.ClientId is { } clientId) return cheques.Where(c => c.Parcel.ClientId == clientId);
        if (caller.AgencyId is { } agencyId) return cheques.Where(c => c.Parcel.AgencyId == agencyId);
        return cheques.Where(c => c.Id == Scope.DenyAllId);
    }

    /// <summary>`scoped` du bon de remise : sans branche vendeur (le vendeur est refusé avant).</summary>
    public static IQueryable<Cheque> SlipScoped(IQueryable<Cheque> cheques, ICurrentUser caller)
    {
        if (caller.IsSuperAdmin()) return cheques;
        if (caller.CourierId is { } courierId) return cheques.Where(c => c.CourierId == courierId);
        if (caller.AgencyId is { } agencyId) return cheques.Where(c => c.Parcel.AgencyId == agencyId);
        return cheques.Where(c => c.Id == Scope.DenyAllId);
    }

    private sealed record Row(string Id, ChequeStatus Status, string Number, decimal Amount, string? Bank, DateOnly? ChequeDate, DateTime ReceivedAt, string PayerName, string ParcelId,
        string ParcelCode, string ClientName, string CourierId, string CourierName, string? InvoiceCode, InvoiceStatus? InvoiceStatus);

    private static IQueryable<Row> Rows(IQueryable<Cheque> cheques) => cheques.Select(c => new Row(c.Id, c.Status, c.Number, c.Amount, c.Bank, c.ChequeDate, c.ReceivedAt, c.PayerName,
        c.ParcelId, c.Parcel.Code, c.Parcel.Client.Name, c.CourierId, c.Courier.FirstName + " " + c.Courier.LastName, c.Invoice != null ? c.Invoice.Code : null,
        c.Invoice != null ? c.Invoice.Status : null));

    private static T Fill<T>(T item, Row row, ICurrentUser caller) where T : ChequeListItemDto
    {
        var merchant = caller.ClientId is not null;
        item.Id = row.Id; item.Status = Money.ToWire(row.Status); item.Number = row.Number; item.Amount = Money.ToCents(row.Amount); item.Bank = row.Bank;
        item.ChequeDate = row.ChequeDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); item.ReceivedAt = row.ReceivedAt; item.PayerName = row.PayerName; item.ParcelId = row.ParcelId;
        item.ParcelCode = row.ParcelCode; item.ClientName = row.ClientName; item.CourierId = merchant ? string.Empty : row.CourierId; item.CourierName = row.CourierName;
        item.InvoiceCode = merchant && row.InvoiceStatus == InvoiceStatus.DRAFT ? null : row.InvoiceCode;
        return item;
    }

    public static async Task<List<ChequeListItemDto>> ListAsync(IQueryable<Cheque> cheques, ICurrentUser caller, CancellationToken cancellationToken) =>
        (await Rows(cheques).ToListAsync(cancellationToken)).Select(r => Fill(new ChequeListItemDto(), r, caller)).ToList();

    public static async Task<ChequeDetailDto> DetailAsync(IAppDbContext db, ICurrentUser caller, string id, CancellationToken cancellationToken)
    {
        var scoped = Scoped(db.Cheques.AsNoTracking(), caller).Where(c => c.Id == id);
        var row = await Rows(scoped).FirstOrDefaultAsync(cancellationToken) ?? throw BusinessException.NotFound($"Chèque introuvable : {id}");
        var extra = await scoped.Select(c => new { c.Note, c.RejectionReason }).FirstAsync(cancellationToken);
        var events = await db.ChequeEvents.AsNoTracking().Where(e => e.ChequeId == id).OrderBy(e => e.CreatedAt)
            .Select(e => new { e.Id, e.Status, e.Kind, e.Comment, e.AuthorName, e.CreatedAt }).ToListAsync(cancellationToken);
        var merchant = caller.ClientId is not null;
        var detail = Fill(new ChequeDetailDto(), row, caller);
        var status = detail.Status;
        detail.Note = merchant ? null : extra.Note;
        detail.RejectionReason = extra.RejectionReason;
        detail.Events = events.Select(e => new ChequeEventDto(e.Id, Money.ToWire(e.Status), e.Kind == "slip" ? "slip" : "transition", merchant ? null : e.Comment,
            merchant ? string.Empty : e.AuthorName ?? string.Empty, e.CreatedAt)).ToList();
        detail.NextStatus = merchant || (caller.CourierId is not null && status != "recu-livreur") ? null : Next(status);
        detail.Rejectable = !merchant && caller.CourierId is null && CanReject(status);
        detail.Cancellable = !merchant && caller.CourierId is null && CanCancel(status);
        return detail;
    }

    public static async Task<(string Id, string Status, string Number, string ParcelId)> LoadAsync(IAppDbContext db, ICurrentUser caller, string id, CancellationToken cancellationToken)
    {
        var cheque = await Scoped(db.Cheques.AsNoTracking(), caller).Where(c => c.Id == id).Select(c => new { c.Id, c.Status, c.Number, c.ParcelId }).FirstOrDefaultAsync(cancellationToken)
                     ?? throw BusinessException.NotFound($"Chèque introuvable : {id}");
        return (cheque.Id, Money.ToWire(cheque.Status), cheque.Number, cheque.ParcelId);
    }

    /// <summary>`write` : le nouveau statut, ses colonnes de date, et l'événement, en une transaction.</summary>
    public static async Task WriteAsync(IAppDbContext db, ICurrentUser caller, string chequeId, string status, string? comment, DateTime now, string? rejectionReason,
        CancellationToken cancellationToken)
    {
        var authorName = await Remittances.AuthorNameAsync(db, caller.UserId, cancellationToken);
        var value = Enum.Parse<ChequeStatus>(Money.FromWire(status));
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var cheque = await db.Cheques.FirstAsync(c => c.Id == chequeId, cancellationToken);
        cheque.Status = value;
        cheque.UpdatedAt = now;
        switch (status)
        {
            case "remis-admin": cheque.HandedOverAt = now; break;
            case "recu-admin": cheque.ReceivedByAdminAt = now; break;
            case "transmis-vendeur": cheque.TransmittedAt = now; break;
            case "recu-vendeur": cheque.MerchantReceivedAt = now; break;
            case "rejete": cheque.RejectedAt = now; cheque.RejectionReason = rejectionReason; break;
            case "annule": cheque.CancelledAt = now; break;
        }
        db.ChequeEvents.Add(new ChequeEvent { ChequeId = chequeId, Status = value, Comment = string.IsNullOrEmpty(comment) ? null : comment, AuthorId = caller.UserId, AuthorName = authorName, CreatedAt = now });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>`returnToDue` : un chèque rejeté ou annulé redevient dû au vendeur (reversements et factures brouillons recalculés).</summary>
    public static async Task ReturnToDueAsync(IAppDbContext db, ICurrentUser caller, string parcelId, string reason, CancellationToken cancellationToken)
    {
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await CodPayoutDrafts.RefreshDraftsForAsync(db, parcelId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        await InvoiceDraftReplay.ChequeLinesAsync(db, caller, parcelId, await Remittances.AuthorNameAsync(db, caller.UserId, cancellationToken), reason, cancellationToken);
    }
}

/// <summary>`chequeListQuerySchema`.</summary>
public sealed record ChequeListFilter(ListQuery Paging, IReadOnlyList<string>? Status, IReadOnlyList<string>? CourierId, IReadOnlyList<string>? ClientId, string? Bank, string? From, string? To);

internal static class ChequeLists
{
    public static IQueryable<Cheque> Filter(IAppDbContext db, ICurrentUser caller, ChequeListFilter filter)
    {
        var where = Cheques.Scoped(db.Cheques.AsNoTracking(), caller);
        if (filter.Status is { } statuses)
        {
            var wanted = statuses.SelectMany(s => s == Cheques.PendingFilter ? ["recu-livreur", "remis-admin", "recu-admin", "transmis-vendeur"] : new[] { s }).Distinct()
                .Select(s => Enum.TryParse<ChequeStatus>(Money.FromWire(s), out var value) ? value : (ChequeStatus?)null).ToList();
            // Un statut inconnu ne sélectionne rien (Speedy le laisse remonter en erreur Prisma).
            var known = wanted.Where(v => v is not null).Select(v => v!.Value).ToList();
            where = where.Where(c => known.Contains(c.Status));
        }
        if (filter.CourierId is { } couriers && caller.MayFilterBy(caller.CourierId)) where = where.Where(c => couriers.Contains(c.CourierId));
        if (filter.ClientId is { } clients && caller.MayFilterBy(caller.ClientId)) where = where.Where(c => clients.Contains(c.Parcel.ClientId));
        if (!string.IsNullOrEmpty(filter.Bank)) { var bank = filter.Bank.ToLower(); where = where.Where(c => c.Bank != null && c.Bank.ToLower().Contains(bank)); }
        var (from, to) = ReturnRows.DayRange(filter.From, filter.To);
        if (from is { } f) where = where.Where(c => c.ReceivedAt >= f);
        if (to is { } t) where = where.Where(c => c.ReceivedAt < t);
        if (filter.Paging.Search is { Length: > 0 } search)
        {
            var needle = search.ToLower();
            where = where.Where(c => c.Number.ToLower().Contains(needle) || c.Parcel.Code.ToLower().Contains(needle));
        }
        return where;
    }
}

/// <summary>`GET /cheques`.</summary>
public sealed record GetChequesQuery(ChequeListFilter Filter) : IRequest<ChequePageDto>;

public sealed class GetChequesValidator : ZodValidator<GetChequesQuery>
{
    public GetChequesValidator()
    {
        RuleFor(x => x.Filter.Bank).MaxLength(120).OverridePropertyName("bank");
    }
}

internal sealed class GetChequesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetChequesQuery, ChequePageDto>
{
    public async Task<ChequePageDto> Handle(GetChequesQuery request, CancellationToken cancellationToken)
    {
        var where = ChequeLists.Filter(db, caller, request.Filter);
        var paging = request.Filter.Paging;
        var total = await where.CountAsync(cancellationToken);
        var sum = await where.SumAsync(c => (decimal?)c.Amount, cancellationToken);
        var items = await Cheques.ListAsync(where.OrderByDescending(c => c.ReceivedAt).Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize), caller, cancellationToken);
        return new ChequePageDto(items, total, paging.Page, paging.PageSize, sum is null ? 0 : Money.ToCents(sum.Value));
    }
}

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

/// <summary>`GET /cheques/:id`.</summary>
public sealed record GetChequeQuery(string Id) : IRequest<ChequeDetailDto>;

internal sealed class GetChequeHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetChequeQuery, ChequeDetailDto>
{
    public Task<ChequeDetailDto> Handle(GetChequeQuery request, CancellationToken cancellationToken) => Cheques.DetailAsync(db, caller, request.Id, cancellationToken);
}

/// <summary>`chequeAdvanceSchema` : une transition ; le commentaire sert aussi de motif au rejet.</summary>
public sealed record ChequeTransitionCommand : IRequest<ChequeDetailDto>
{
    [JsonIgnore]
    public string Id { get; init; } = string.Empty;

    [JsonIgnore]
    public string Action { get; init; } = string.Empty;

    public string? Comment { get; init => field = Zod.Trim(value); }
}

public sealed class ChequeTransitionValidator : ZodValidator<ChequeTransitionCommand>
{
    public ChequeTransitionValidator()
    {
        RuleFor(x => x.Comment).MaxLength(500);
    }
}

internal sealed class ChequeTransitionHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<ChequeTransitionCommand, ChequeDetailDto>
{
    public async Task<ChequeDetailDto> Handle(ChequeTransitionCommand input, CancellationToken cancellationToken)
    {
        if (caller.ClientId is not null) throw BusinessException.Forbidden("Le suivi de vos chèques est en lecture seule.", new { reason = "cheque-is-agency-only" });
        var now = DateTime.UtcNow;
        switch (input.Action)
        {
            case "advance":
            {
                var cheque = await Cheques.LoadAsync(db, caller, input.Id, cancellationToken);
                if (caller.CourierId is not null && cheque.Status != "recu-livreur")
                {
                    throw BusinessException.Forbidden("Le chèque n’est plus dans votre sacoche : seule l’agence le fait avancer.", new { reason = "cheque-is-agency-only" });
                }
                var next = Cheques.Next(cheque.Status) ?? throw BusinessException.Conflict("Ce chèque est au bout de son parcours.", new { reason = "cheque-completed" });
                await Cheques.WriteAsync(db, caller, cheque.Id, next, input.Comment, now, null, cancellationToken);
                break;
            }
            case "reject":
            {
                if (caller.CourierId is not null) throw BusinessException.Forbidden("Seule l’agence constate un rejet.", new { reason = "cheque-is-agency-only" });
                var cheque = await Cheques.LoadAsync(db, caller, input.Id, cancellationToken);
                if (!Cheques.CanReject(cheque.Status)) throw BusinessException.Conflict("Ce chèque ne peut pas être rejeté à ce stade.", new { reason = "cheque-wrong-status", status = cheque.Status });
                var reason = input.Comment?.Trim();
                if (string.IsNullOrEmpty(reason)) throw BusinessException.Validation("Un chèque rejeté demande un motif.", new { field = "comment", reason = "cheque-reason-required" });
                await Cheques.WriteAsync(db, caller, cheque.Id, "rejete", reason, now, reason, cancellationToken);
                await Cheques.ReturnToDueAsync(db, caller, cheque.ParcelId, $"Chèque rejeté : {reason}", cancellationToken);
                break;
            }
            default:
            {
                if (caller.CourierId is not null) throw BusinessException.Forbidden("Seule l’agence annule un chèque.", new { reason = "cheque-is-agency-only" });
                var cheque = await Cheques.LoadAsync(db, caller, input.Id, cancellationToken);
                if (!Cheques.CanCancel(cheque.Status))
                {
                    throw BusinessException.Conflict("Ce chèque est encaissé ou déjà sorti : il ne s’annule plus.", new { reason = "cheque-wrong-status", status = cheque.Status });
                }
                await Cheques.WriteAsync(db, caller, cheque.Id, "annule", input.Comment, now, null, cancellationToken);
                await Cheques.ReturnToDueAsync(db, caller, cheque.ParcelId, string.IsNullOrWhiteSpace(input.Comment) ? "Chèque annulé." : input.Comment.Trim(), cancellationToken);
                break;
            }
        }
        db.ChangeTracker.Clear();
        return await Cheques.DetailAsync(db, caller, input.Id, cancellationToken);
    }
}

/// <summary>`chequeBulkSchema` : une pile de chèques vers l'étape suivante ; les autres sont écartés, motivés.</summary>
public sealed record AdvanceChequesCommand : IRequest<ChequeBulkResultDto>
{
    public IReadOnlyList<string>? Ids { get; init; }
    public string? Status { get; init; }
    public string? Comment { get; init => field = Zod.Trim(value); }
}

public sealed class AdvanceChequesValidator : ZodValidator<AdvanceChequesCommand>
{
    public AdvanceChequesValidator()
    {
        RuleFor(x => x.Ids).Required("array").Custom((ids, context) =>
        {
            if (ids is null) return;
            if (ids.Count < 1) context.AddFailure(ValidationPaths.Raw + "ids", "required");
            if (ids.Count > NoteRules.SelectionMax) context.AddFailure(ValidationPaths.Raw + "ids", "too-many");
            for (var i = 0; i < ids.Count; i++) if (string.IsNullOrEmpty(ids[i])) context.AddFailure(ValidationPaths.Raw + $"ids.{i}", Zod.TooSmallString(1));
        });
        RuleFor(x => x.Status).Required().OneOf(Cheques.Statuses);
        RuleFor(x => x.Comment).MaxLength(500);
    }
}

internal sealed class AdvanceChequesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<AdvanceChequesCommand, ChequeBulkResultDto>
{
    public async Task<ChequeBulkResultDto> Handle(AdvanceChequesCommand input, CancellationToken cancellationToken)
    {
        if (caller.CourierId is not null || caller.ClientId is not null)
        {
            throw BusinessException.Forbidden("Seule l’agence fait avancer une pile de chèques.", new { reason = "cheque-is-agency-only" });
        }
        var done = new List<string>();
        var skipped = new List<ChequeBulkSkipDto>();
        foreach (var id in input.Ids!)
        {
            try
            {
                var cheque = await Cheques.LoadAsync(db, caller, id, cancellationToken);
                if (Cheques.Next(cheque.Status) != input.Status)
                {
                    skipped.Add(new ChequeBulkSkipDto(id, cheque.Number, "cheque-wrong-status", cheque.Status));
                    continue;
                }
                await Cheques.WriteAsync(db, caller, cheque.Id, input.Status!, input.Comment, DateTime.UtcNow, null, cancellationToken);
                done.Add(id);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                db.ChangeTracker.Clear();
                skipped.Add(new ChequeBulkSkipDto(id, null, "cheque-unreachable", null));
            }
        }
        return new ChequeBulkResultDto(done, skipped);
    }
}

/// <summary>`ChequeSlipService` : en-tête des bons de remise (identifiant fiscal Pro, logo assombri).</summary>
internal static class ChequeSlips
{
    public const string Prefix = "BRC";

    public static async Task<(PdfCompany Company, byte[]? Logo)> LetterheadAsync(IAppDbContext db, IFileStorage storage, CancellationToken cancellationToken)
    {
        var company = await PdfCompany.LoadAsync(db, "Bon de remise", cancellationToken);
        var identity = await db.Companies.AsNoTracking().OrderBy(c => c.CreatedAt).Select(c => new { c.Id, c.ProTaxId, c.LogoUrl }).FirstOrDefaultAsync(cancellationToken);
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
        var taxId = identity?.ProTaxId?.Trim() is { Length: > 0 } pro ? pro : company.TaxId?.Trim() is { Length: > 0 } own ? own : null;
        return (company with { TaxId = taxId }, logo);
    }
}

/// <summary>`POST /cheques/:id/slip` : le bon de remise d'un chèque (code BRC-&lt;colis&gt;), tracé dans son journal.</summary>
public sealed record GetChequeSlipQuery(string Id) : IRequest<FileContent>;

internal sealed class GetChequeSlipHandler(IAppDbContext db, ICurrentUser caller, IPdfService pdf, IFileStorage storage, IClock clock) : IRequestHandler<GetChequeSlipQuery, FileContent>
{
    public async Task<FileContent> Handle(GetChequeSlipQuery request, CancellationToken cancellationToken)
    {
        if (caller.ClientId is not null) throw BusinessException.Forbidden("Le bon de remise est édité par l’agence.");
        var cheque = await Cheques.SlipScoped(db.Cheques.AsNoTracking(), caller).Where(c => c.Id == request.Id)
                         .Select(c => new
                         {
                             c.Id, c.Status, c.Number, c.Bank, c.Amount, ParcelCode = c.Parcel.Code, c.Parcel.RecipientName, Client = c.Parcel.Client.Name,
                             Courier = c.Courier.FirstName + " " + c.Courier.LastName,
                         }).FirstOrDefaultAsync(cancellationToken)
                     ?? throw BusinessException.NotFound($"Chèque introuvable : {request.Id}");
        var (company, logo) = await ChequeSlips.LetterheadAsync(db, storage, cancellationToken);
        var code = $"{ChequeSlips.Prefix}-{cheque.ParcelCode}";
        var printedAt = clock.UtcNow;
        var content = pdf.Render(new ChequeSlipSheet(company, logo, code, printedAt, cheque.Number, cheque.Bank, Money.ToCents(cheque.Amount), cheque.ParcelCode, cheque.Client, cheque.Courier,
            cheque.RecipientName));
        db.ChequeEvents.Add(new ChequeEvent
        {
            ChequeId = cheque.Id, Status = cheque.Status, Kind = "slip", Comment = $"Bon de remise {code} téléchargé", AuthorId = caller.UserId,
            AuthorName = await Remittances.AuthorNameAsync(db, caller.UserId, cancellationToken), CreatedAt = printedAt,
        });
        await db.SaveChangesAsync(cancellationToken);
        return new FileContent(content, FileContent.Pdf, $"{code}.pdf", CacheControl: "private, no-store");
    }
}

/// <summary>`chequeGroupSlipSchema` : un bon de remise par vendeur (BRC-), en un seul PDF.</summary>
public sealed record GetChequeGroupSlipQuery : IRequest<FileContent>
{
    public IReadOnlyList<string>? Ids { get; init; }
}

public sealed class GetChequeGroupSlipValidator : ZodValidator<GetChequeGroupSlipQuery>
{
    public GetChequeGroupSlipValidator()
    {
        RuleFor(x => x.Ids).Required("array").Custom((ids, context) =>
        {
            if (ids is null) return;
            if (ids.Count < 1) context.AddFailure(ValidationPaths.Raw + "ids", "required");
            if (ids.Count > 500) context.AddFailure(ValidationPaths.Raw + "ids", "too-many");
            for (var i = 0; i < ids.Count; i++) if (string.IsNullOrEmpty(ids[i])) context.AddFailure(ValidationPaths.Raw + $"ids.{i}", Zod.TooSmallString(1));
        });
    }
}

internal sealed class GetChequeGroupSlipHandler(IAppDbContext db, ICurrentUser caller, IPdfService pdf, IFileStorage storage, IClock clock) : IRequestHandler<GetChequeGroupSlipQuery, FileContent>
{
    public async Task<FileContent> Handle(GetChequeGroupSlipQuery request, CancellationToken cancellationToken)
    {
        if (caller.ClientId is not null) throw BusinessException.Forbidden("Le bon de remise est édité par l’agence.");
        var ids = request.Ids!.Distinct().ToList();
        var cheques = await Cheques.SlipScoped(db.Cheques.AsNoTracking(), caller).Where(c => ids.Contains(c.Id))
            .Select(c => new
            {
                c.Id, c.Status, c.Number, c.Bank, c.Amount, c.PayerName, c.CourierId, ParcelCode = c.Parcel.Code, ClientId = c.Parcel.Client.Id, Client = c.Parcel.Client.Name,
                Courier = c.Courier.FirstName + " " + c.Courier.LastName,
            }).ToListAsync(cancellationToken);
        if (cheques.Count != ids.Count) throw BusinessException.NotFound("Un ou plusieurs chèques sont introuvables ou hors de portée.");
        var french = StringComparer.Create(CultureInfo.GetCultureInfo("fr-FR"), CompareOptions.None);
        var groups = cheques.GroupBy(c => c.ClientId).Select(g => new { Name = g.First().Client, Cheques = g.OrderBy(c => c.ParcelCode, StringComparer.Create(CultureInfo.InvariantCulture, false)).ToList() })
            .OrderBy(g => g.Name, french).ToList();
        var (company, logo) = await ChequeSlips.LetterheadAsync(db, storage, cancellationToken);
        var printedAt = clock.UtcNow;
        var codes = new List<string>();
        foreach (var _ in groups) codes.Add(await DocumentCodes.ReserveAsync(db, ChequeSlips.Prefix, printedAt, cancellationToken));
        var slips = groups.Select((g, index) => new ChequeGroupSlip(codes[index], g.Name, g.Cheques.Select(c => c.CourierId).Distinct().Count() == 1 ? g.Cheques[0].Courier : null,
            g.Cheques.Select(c => new ChequeGroupSlipLine(c.Number, c.Bank, Money.ToCents(c.Amount), c.ParcelCode, c.Courier, c.PayerName)).ToList())).ToList();
        var content = pdf.Render(new ChequeGroupSlipSheet(company, logo, printedAt, slips));
        var authorName = await Remittances.AuthorNameAsync(db, caller.UserId, cancellationToken);
        for (var i = 0; i < groups.Count; i++)
        {
            foreach (var cheque in groups[i].Cheques)
            {
                db.ChequeEvents.Add(new ChequeEvent
                {
                    ChequeId = cheque.Id, Status = cheque.Status, Kind = "slip", Comment = $"Bon de remise groupé {codes[i]} téléchargé", AuthorId = caller.UserId, AuthorName = authorName,
                    CreatedAt = printedAt,
                });
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        var fileName = codes.Count == 1 ? $"{codes[0]}.pdf" : $"{codes[0]}-{codes.Count}-bons.pdf";
        return new FileContent(content, FileContent.Pdf, fileName, CacheControl: "private, no-store");
    }
}
