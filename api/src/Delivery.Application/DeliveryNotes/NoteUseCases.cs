using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.Parcels.Commands;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.DeliveryNotes;

/// <summary>`GET /delivery-notes`.</summary>
public sealed record GetNotesQuery(NoteListFilter Filter) : IRequest<Paginated<DeliveryNoteListItemDto>>;

internal sealed class GetNotesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetNotesQuery, Paginated<DeliveryNoteListItemDto>>
{
    public async Task<Paginated<DeliveryNoteListItemDto>> Handle(GetNotesQuery request, CancellationToken cancellationToken)
    {
        var paging = request.Filter.Paging;
        var policy = await NoteRules.PolicyAsync(db, caller.AgencyId, cancellationToken);
        var where = NoteRows.Filter(db.DeliveryNotes.AsNoTracking(), caller, request.Filter);
        var total = await where.CountAsync(cancellationToken);
        var rows = await NoteRows.ProjectAsync(where.OrderByDescending(n => n.CreatedAt).Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize), cancellationToken);
        return new Paginated<DeliveryNoteListItemDto>(rows.Select(r => NoteRows.Fill(new DeliveryNoteListItemDto(), r, policy)).ToList(), total, paging.Page, paging.PageSize);
    }
}

public sealed record DeliveryNoteStatsDto(int Total, int Awaiting, int Parcels, int Waived, long PickupFee, long FreePickupThreshold);

/// <summary>`GET /delivery-notes/stats` : bons, en attente, colis, ramassages offerts, conditions de ramassage.</summary>
public sealed record GetNoteStatsQuery(NoteListFilter Filter) : IRequest<DeliveryNoteStatsDto>;

internal sealed class GetNoteStatsHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetNoteStatsQuery, DeliveryNoteStatsDto>
{
    public async Task<DeliveryNoteStatsDto> Handle(GetNoteStatsQuery request, CancellationToken cancellationToken)
    {
        var where = NoteRows.Filter(db.DeliveryNotes.AsNoTracking(), caller, request.Filter);
        var policy = await NoteRules.PolicyAsync(db, caller.AgencyId, cancellationToken);
        var total = await where.CountAsync(cancellationToken);
        var awaiting = await where.CountAsync(n => n.Status != DeliveryNoteStatus.VALIDATED, cancellationToken);
        var parcels = await where.SelectMany(n => n.Parcels).CountAsync(cancellationToken);
        var pickups = await where.Where(n => n.Type == DeliveryNoteType.PICKUP)
            .Select(n => new { n.Status, n.PickupFeeApplied, n.PickupThresholdApplied, n.Client.PickupFee, n.Client.FreePickupThreshold, Count = n.Parcels.Count }).ToListAsync(cancellationToken);
        var waived = pickups.Count(n => n.Count >= NoteRules.RuleOf(n.Status, n.PickupFeeApplied, n.PickupThresholdApplied, n.PickupFee, n.FreePickupThreshold, policy).FreeFrom);
        var rule = policy;
        if (caller.ClientId is { } clientId)
        {
            var client = await db.Clients.AsNoTracking().Where(c => c.Id == clientId).Select(c => new { c.PickupFee, c.FreePickupThreshold }).FirstOrDefaultAsync(cancellationToken);
            rule = NoteRules.EffectivePickupPolicy(policy, client?.PickupFee, client?.FreePickupThreshold);
        }
        return new DeliveryNoteStatsDto(total, awaiting, parcels, waived, rule.PickupFee, rule.FreePickupThreshold);
    }
}

/// <summary>`GET /delivery-notes/export` : classeur « Bons ».</summary>
public sealed record ExportNotesQuery(NoteListFilter Filter) : IRequest<FileContent>;

internal sealed class ExportNotesHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportNotesQuery, FileContent>
{
    private static readonly IReadOnlyList<ExportColumn<DeliveryNoteListItemDto>> Columns =
    [
        new("Bon", r => r.Code),
        new("Type", r => DocumentLabels.Enum(r.Kind)),
        new("Statut", r => DocumentLabels.Enum(r.Status)),
        new("E-commerçant", r => r.ClientName),
        new("Ville", r => r.CityName),
        new("Colis", r => r.ParcelCount),
        new("Ramassage offert", r => Exports.Bool(r.PickupWaived)),
        new("Créé le", r => Exports.DateTimeText(r.CreatedAt)),
        new("Validé le", r => Exports.DateTimeText(r.ValidatedAt)),
    ];

    public async Task<FileContent> Handle(ExportNotesQuery request, CancellationToken cancellationToken)
    {
        var policy = await NoteRules.PolicyAsync(db, caller.AgencyId, cancellationToken);
        var where = NoteRows.Filter(db.DeliveryNotes.AsNoTracking(), caller, request.Filter);
        Exports.AssertWithinLimit(await where.CountAsync(cancellationToken));
        var rows = (await NoteRows.ProjectAsync(where.OrderByDescending(n => n.CreatedAt), cancellationToken)).Select(r => NoteRows.Fill(new DeliveryNoteListItemDto(), r, policy)).ToList();
        return await Exports.ToXlsxAsync(db, excel, clock, "Bons", Columns, rows, cancellationToken);
    }
}

/// <summary>`GET /delivery-notes/:code`.</summary>
public sealed record GetNoteQuery(string Code) : IRequest<DeliveryNoteDetailDto>;

internal sealed class GetNoteHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetNoteQuery, DeliveryNoteDetailDto>
{
    public Task<DeliveryNoteDetailDto> Handle(GetNoteQuery request, CancellationToken cancellationToken) => NoteRows.DetailAsync(db, caller, request.Code, cancellationToken);
}

/// <summary>`GET /delivery-notes/:code/export` : les colis du bon (sans les frais pour un livreur).</summary>
public sealed record ExportNoteParcelsQuery(string Code) : IRequest<FileContent>;

internal sealed class ExportNoteParcelsHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportNoteParcelsQuery, FileContent>
{
    private sealed record Line(NoteParcelDto Parcel, string? ExternalCode, string RecipientPhone);

    public async Task<FileContent> Handle(ExportNoteParcelsQuery request, CancellationToken cancellationToken)
    {
        var note = await NoteRows.DetailAsync(db, caller, request.Code, cancellationToken);
        var ids = note.Parcels.Select(p => p.Id).ToList();
        var extras = await db.Parcels.AsNoTracking().Where(p => ids.Contains(p.Id)).Select(p => new { p.Id, p.ExternalCode, p.RecipientPhone }).ToDictionaryAsync(p => p.Id, cancellationToken);
        var columns = new List<ExportColumn<Line>>
        {
            new("Code", r => r.Parcel.Code, Text: true),
            new("Référence commande", r => r.ExternalCode ?? string.Empty, Text: true),
            new("Destinataire", r => r.Parcel.RecipientName),
            new("Téléphone", r => r.RecipientPhone, Text: true),
            new("Ville", r => r.Parcel.DestinationCityName),
            new("Quartier", r => r.Parcel.DestinationDistrictName ?? string.Empty),
            new("Désignation", r => r.Parcel.Description),
            new("Statut", r => DocumentLabels.ParcelStatus(r.Parcel.StatusId)),
            new("Contre-remboursement", r => Exports.Amount(r.Parcel.CodAmount)),
            new("Frais de livraison", r => Exports.Amount(r.Parcel.DeliveryFee)),
            new("Cartons", r => r.Parcel.Quantity),
            new("Client corrigé", r => NoteRows.ClientCorrectionText(r.Parcel.ClientCorrection)),
            new("Motif de la correction", r => r.Parcel.ClientCorrection?.Reason ?? string.Empty),
        };
        if (caller.CourierId is not null) columns.RemoveAll(c => c.Header == "Frais de livraison");
        var rows = note.Parcels.Select(p => new Line(p, extras.GetValueOrDefault(p.Id)?.ExternalCode, extras.GetValueOrDefault(p.Id)?.RecipientPhone ?? string.Empty)).ToList();
        return await Exports.ToXlsxAsync(db, excel, clock, $"Bon {note.Code}", columns, rows, cancellationToken);
    }
}

/// <summary>`deliveryNoteSchema`.</summary>
public sealed record CreateNoteCommand : IRequest<DeliveryNoteDetailDto>
{
    public string? Kind { get; init; } = "pickup";
    public string? ClientId { get; init; }
    public string? AgencyId { get; init; }
    public string? LocationCityId { get; init; }
}

public sealed class CreateNoteValidator : ZodValidator<CreateNoteCommand>
{
    public CreateNoteValidator()
    {
        RuleFor(x => x.Kind).OneOf(["pickup", "dropoff"]);
        RuleFor(x => x.ClientId).Required().MinLength(1, "required");
        RuleFor(x => x.AgencyId).Required().MinLength(1, "required");
        RuleFor(x => x.LocationCityId).Must(v => v is null || v.Length >= 0).WithMessage(Zod.InvalidInput);
    }
}

/// <summary>`createNote` / `createNoteWithParcels`.</summary>
public static class NoteCreation
{
    public static async Task<string> CreateAsync(IAppDbContext db, ICurrentUser caller, DeliveryNoteType type, string clientId, string agencyId, string? locationCityId,
        IReadOnlyList<string>? parcelIds, CancellationToken cancellationToken)
    {
        if (parcelIds is { Count: 0 }) throw BusinessException.Validation("Un bon sans colis ne s’ouvre pas.", new { field = "parcelIds" });
        var client = await db.Clients.AsNoTracking().Where(c => c.Id == clientId && c.DeletedAt == null).Select(c => new { c.Id, c.CityId }).FirstOrDefaultAsync(cancellationToken)
                     ?? throw BusinessException.NotFound("E-commerçant introuvable.");
        if (!await db.Agencies.AnyAsync(a => a.Id == agencyId && a.DeletedAt == null, cancellationToken)) throw BusinessException.NotFound("Agence introuvable.");
        var parcels = parcelIds is null ? null : await NoteRules.AssertParcelsAsync(db, clientId, type, parcelIds, null, cancellationToken);
        var now = DateTime.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (parcels is not null) await NoteRules.LockFreeOfNotesAsync(db, parcels.Select(p => p.Id).ToList(), null, cancellationToken);
        var code = await DocumentCodes.ReserveAsync(db, type == DeliveryNoteType.PICKUP ? NoteRules.PickupPrefix : NoteRules.DropoffPrefix, now, cancellationToken);
        var note = new DeliveryNote
        {
            Code = code,
            Type = type,
            Status = DeliveryNoteStatus.AWAITING_VALIDATION,
            ClientId = clientId,
            AgencyId = agencyId,
            LocationCityId = string.IsNullOrEmpty(locationCityId) ? client.CityId : locationCityId,
            CreatedAt = now,
        };
        db.DeliveryNotes.Add(note);
        if (parcels is not null)
        {
            foreach (var parcel in parcels) db.DeliveryNoteParcels.Add(new DeliveryNoteParcel { DeliveryNoteId = note.Id, ParcelId = parcel.Id });
            await NoteRules.AlignParcelsAsync(db, caller, parcels, type, code, note.LocationCityId, now, cancellationToken);
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return code;
    }
}

internal sealed class CreateNoteHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<CreateNoteCommand, DeliveryNoteDetailDto>
{
    public async Task<DeliveryNoteDetailDto> Handle(CreateNoteCommand input, CancellationToken cancellationToken)
    {
        var code = await NoteCreation.CreateAsync(db, caller, input.Kind == "dropoff" ? DeliveryNoteType.DROPOFF : DeliveryNoteType.PICKUP, input.ClientId!, input.AgencyId!, input.LocationCityId, null, cancellationToken);
        return await NoteRows.DetailAsync(db, caller, code, cancellationToken);
    }
}

/// <summary>`selectionByCriteria` + `optionalIds` : des colis cochés, ou « tout » (recherche) moins des exclus.</summary>
public abstract record ParcelSelection
{
    public IReadOnlyList<string>? ParcelIds { get; init; }
    public bool? All { get; init; }
    public string? Search { get; init => field = Zod.Trim(value); }
    public IReadOnlyList<string>? Exclude { get; init; }
}

public abstract class ParcelSelectionValidator<T> : ZodValidator<T>
    where T : ParcelSelection
{
    protected ParcelSelectionValidator()
    {
        RuleFor(x => x.ParcelIds).Custom((ids, context) => Ids(ids, "parcelIds", context));
        RuleFor(x => x.Exclude).Custom((ids, context) => Ids(ids, "exclude", context));
        RuleFor(x => x.Search).MaxLength(120);
    }

    private static void Ids(IReadOnlyList<string>? ids, string path, ValidationContext<T> context)
    {
        if (ids is null) return;
        for (var i = 0; i < ids.Count; i++) if (string.IsNullOrEmpty(ids[i])) context.AddFailure(ValidationPaths.Raw + $"{path}.{i}", Zod.TooSmallString(1));
        if (ids.Count > NoteRules.SelectionMax) context.AddFailure(ValidationPaths.Raw + path, "too-many");
    }

    protected override void Refine(T value, Action<string, string> issue)
    {
        if (value.All != true && (value.ParcelIds?.Count ?? 0) == 0) issue(ValidationPaths.Raw + "parcelIds", "required");
    }
}

/// <summary>`POST /delivery-notes/:code/parcels` (`deliveryNoteParcelsSchema`).</summary>
public sealed record AddNoteParcelsCommand : ParcelSelection, IRequest<DeliveryNoteDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;
}

public sealed class AddNoteParcelsValidator : ParcelSelectionValidator<AddNoteParcelsCommand>;

internal sealed class AddNoteParcelsHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<AddNoteParcelsCommand, DeliveryNoteDetailDto>
{
    public async Task<DeliveryNoteDetailDto> Handle(AddNoteParcelsCommand input, CancellationToken cancellationToken)
    {
        var note = await NoteRows.Scoped(db.DeliveryNotes.AsNoTracking(), caller).FirstOrDefaultAsync(n => n.Code == input.Code, cancellationToken)
                   ?? throw BusinessException.NotFound($"Bon introuvable : {input.Code}");
        if (note.Status == DeliveryNoteStatus.VALIDATED) throw BusinessException.Conflict("Ce bon est validé : sa composition ne change plus.", new { reason = "note-validated" });
        var ids = await NoteRules.ResolveSelectionAsync(input.ParcelIds, input.All, input.Exclude, take => NoteRules.CandidateIdsAsync(db, note.ClientId, input.Search, take, cancellationToken));
        var parcels = await NoteRules.AssertParcelsAsync(db, note.ClientId, note.Type, ids, note.Id, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await NoteRules.LockFreeOfNotesAsync(db, parcels.Select(p => p.Id).ToList(), note.Id, cancellationToken);
        var existing = (await db.DeliveryNoteParcels.Where(l => l.DeliveryNoteId == note.Id).Select(l => l.ParcelId).ToListAsync(cancellationToken)).ToHashSet();
        foreach (var parcel in parcels.Where(p => !existing.Contains(p.Id))) db.DeliveryNoteParcels.Add(new DeliveryNoteParcel { DeliveryNoteId = note.Id, ParcelId = parcel.Id });
        await NoteRules.AlignParcelsAsync(db, caller, parcels, note.Type, note.Code, note.LocationCityId, DateTime.UtcNow, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await NoteRows.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}

/// <summary>`DELETE /delivery-notes/:code/parcels/:parcelId`.</summary>
public sealed record RemoveNoteParcelCommand(string Code, string ParcelId) : IRequest<DeliveryNoteDetailDto>;

internal sealed class RemoveNoteParcelHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<RemoveNoteParcelCommand, DeliveryNoteDetailDto>
{
    public async Task<DeliveryNoteDetailDto> Handle(RemoveNoteParcelCommand request, CancellationToken cancellationToken)
    {
        var note = await NoteRows.Scoped(db.DeliveryNotes.AsNoTracking(), caller).Where(n => n.Code == request.Code).Select(n => new { n.Id, n.Status }).FirstOrDefaultAsync(cancellationToken)
                   ?? throw BusinessException.NotFound($"Bon introuvable : {request.Code}");
        if (note.Status == DeliveryNoteStatus.VALIDATED) throw BusinessException.Conflict("Ce bon est validé : sa composition ne change plus.");
        db.DeliveryNoteParcels.RemoveRange(await db.DeliveryNoteParcels.Where(l => l.DeliveryNoteId == note.Id && l.ParcelId == request.ParcelId).ToListAsync(cancellationToken));
        await db.SaveChangesAsync(cancellationToken);
        return await NoteRows.DetailAsync(db, caller, request.Code, cancellationToken);
    }
}

/// <summary>`DELETE /delivery-notes/:code` : ni validé, ni chez le livreur, ni sur un circuit.</summary>
public sealed record DeleteNoteCommand(string Code) : IRequest<NoteCodeDto>;

public sealed record NoteCodeDto(string Code);

internal sealed class DeleteNoteHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<DeleteNoteCommand, NoteCodeDto>
{
    public Task<NoteCodeDto> Handle(DeleteNoteCommand request, CancellationToken cancellationToken) => NoteDeletion.DeleteAsync(db, caller, request.Code, cancellationToken);
}

/// <summary>`deleteNote`.</summary>
public static class NoteDeletion
{
    public static async Task<NoteCodeDto> DeleteAsync(IAppDbContext db, ICurrentUser caller, string code, CancellationToken cancellationToken)
    {
        var note = await NoteRows.Scoped(db.DeliveryNotes, caller).Include(n => n.Parcels).Include(n => n.RemovedParcels).FirstOrDefaultAsync(n => n.Code == code, cancellationToken)
                   ?? throw BusinessException.NotFound($"Bon introuvable : {code}");
        if (note.Status == DeliveryNoteStatus.VALIDATED) throw BusinessException.Conflict("Ce bon est validé : il ne se supprime plus.", new { reason = "note-validated" });
        if (note.Status == DeliveryNoteStatus.PICKED_UP_BY_COURIER) throw BusinessException.Conflict("Ce bon est entre les mains du livreur : il ne se supprime plus.", new { reason = "note-picked-up" });
        if (await db.RouteNotes.AnyAsync(l => l.DeliveryNoteId == note.Id, cancellationToken)) throw BusinessException.Conflict("Ce bon est porté par un circuit : retirez-l’en d’abord.", new { reason = "note-in-route" });
        db.DeliveryNoteParcels.RemoveRange(note.Parcels);
        db.DeliveryNoteRemovedParcels.RemoveRange(note.RemovedParcels);
        db.DeliveryNotes.Remove(note);
        await db.SaveChangesAsync(cancellationToken);
        return new NoteCodeDto(code);
    }
}

/// <summary>`POST /delivery-notes/:code/removed/:parcelId/restore` : un colis retiré par erreur revient en attente de ramassage.</summary>
public sealed record RestoreNoteParcelCommand(string Code, string ParcelId) : IRequest<DeliveryNoteDetailDto>;

internal sealed class RestoreNoteParcelHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<RestoreNoteParcelCommand, DeliveryNoteDetailDto>
{
    public async Task<DeliveryNoteDetailDto> Handle(RestoreNoteParcelCommand request, CancellationToken cancellationToken)
    {
        var note = await NoteRows.Scoped(db.DeliveryNotes.AsNoTracking(), caller).FirstOrDefaultAsync(n => n.Code == request.Code, cancellationToken)
                   ?? throw BusinessException.NotFound($"Bon introuvable : {request.Code}");
        var removal = await db.DeliveryNoteRemovedParcels.Include(r => r.Parcel).FirstOrDefaultAsync(r => r.DeliveryNoteId == note.Id && r.ParcelId == request.ParcelId, cancellationToken)
                      ?? throw BusinessException.NotFound("Ce colis n’a pas été retiré de ce bon.");
        if (removal.RestoredAt is not null) throw BusinessException.Conflict("Ce colis est déjà restauré.", new { reason = "already-restored" });
        var parcel = removal.Parcel;
        if (parcel.DeletedAt is null)
        {
            throw BusinessException.Conflict($"Le colis {parcel.Code} n’a pas été supprimé : il est déjà en attente de ramassage.", new { reason = "not-deleted" });
        }
        var restorable = note.Status != DeliveryNoteStatus.VALIDATED
                         && (removal.RouteId is null || await db.Routes.Where(r => r.Id == removal.RouteId).Select(r => r.Status).FirstOrDefaultAsync(cancellationToken) != RouteStatus.CLOSED);
        if (!restorable)
        {
            throw BusinessException.Validation($"{note.Code} est validé : un colis retiré ne se restaure plus. S’il est retrouvé, recréez-le ; il partira sur un nouveau bon.", new { reason = "note-restore-locked" });
        }
        var now = DateTime.UtcNow;
        var author = await ParcelStatusChange.AuthorNameAsync(db, caller.UserId, cancellationToken);
        var deletedAt = parcel.DeletedAt;
        parcel.DeletedAt = null;
        parcel.CourierId = null;
        if (parcel.StatusId != NoteRules.AwaitingPickup)
        {
            parcel.StatusId = NoteRules.AwaitingPickup;
            parcel.StatusAt = now;
            db.ParcelEvents.Add(new ParcelEvent { ParcelId = parcel.Id, StatusId = NoteRules.AwaitingPickup, Comment = NoteRules.RestoreReason(note.Code), AuthorId = caller.UserId, CityId = note.LocationCityId, CreatedAt = now });
        }
        if (parcel.PickupWaived) parcel.PickupWaived = false;
        removal.RestoredAt = now;
        removal.RestoredBy = caller.UserId;
        ParcelJournal.Record(db, caller, parcel.Id, "deletedAt", ParcelAdmin.Iso(deletedAt!.Value), string.Empty, NoteRules.RestoreReason(note.Code), author, now);
        await db.SaveChangesAsync(cancellationToken);
        return await NoteRows.DetailAsync(db, caller, request.Code, cancellationToken);
    }
}

/// <summary>`deliveryNoteValidateSchema` : sans liste, tout le bon ; avec, seuls ceux-là (les autres sont retirés).</summary>
public sealed record ValidateNoteCommand : IRequest<object>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    [JsonIgnore]
    public bool Async { get; init; }

    public IReadOnlyList<string>? ParcelIds { get; init; }
}

public sealed class ValidateNoteValidator : ZodValidator<ValidateNoteCommand>
{
    public ValidateNoteValidator()
    {
        RuleFor(x => x.ParcelIds).Custom((ids, context) =>
        {
            if (ids is null) return;
            if (ids.Count < 1) context.AddFailure(ValidationPaths.Raw + "parcelIds", "required");
            if (ids.Count > NoteRules.SelectionMax) context.AddFailure(ValidationPaths.Raw + "parcelIds", "too-many");
            for (var i = 0; i < ids.Count; i++) if (string.IsNullOrEmpty(ids[i])) context.AddFailure(ValidationPaths.Raw + $"parcelIds.{i}", Zod.TooSmallString(1));
        });
    }
}

/// <summary>`validateNote` : en une fois, ou par morceaux depuis une tâche de masse.</summary>
public static class NoteValidation
{
    public sealed record Options(bool ParcelsPhase = false, bool FinishPhase = false, IReadOnlyList<string>? ChunkIds = null, int? PickedUpCount = null, int? RemovedTotal = null);

    public sealed record ChunkResult(int Removed, int Validated);

    public static async Task<ChunkResult> RunAsync(IAppDbContext db, ICurrentUser caller, ILogger logger, string code, IReadOnlyList<string>? keptIds, Options options, CancellationToken cancellationToken)
    {
        var note = await NoteRows.Scoped(db.DeliveryNotes, caller).FirstOrDefaultAsync(n => n.Code == code, cancellationToken)
                   ?? throw BusinessException.NotFound($"Bon introuvable : {code}");
        var links = await db.DeliveryNoteParcels.Where(l => l.DeliveryNoteId == note.Id).Include(l => l.Parcel).ToListAsync(cancellationToken);
        if (links.Count == 0) throw BusinessException.Validation("Un bon sans colis ne se valide pas.");
        var kept = keptIds?.ToHashSet();
        if (kept is not null && kept.Any(id => links.All(l => l.ParcelId != id)))
        {
            throw BusinessException.Validation($"Un colis coché n'appartient pas au bon {note.Code}.", new { reason = "parcel-not-in-note", noteCode = note.Code });
        }
        var chunk = options.ParcelsPhase ? (options.ChunkIds ?? []).ToHashSet() : null;
        bool InChunk(DeliveryNoteParcel l) => chunk is null || chunk.Contains(l.ParcelId);
        var removed = kept is null ? [] : links.Where(l => !kept.Contains(l.ParcelId) && InChunk(l)).ToList();
        var openRouteIds = await NoteRules.OpenRoutesOfNoteAsync(db, note.Id, cancellationToken);
        if (note.Status == DeliveryNoteStatus.VALIDATED && (removed.Count == 0 || openRouteIds.Count == 0)) throw BusinessException.Conflict("Ce bon est déjà validé.");
        var now = DateTime.UtcNow;
        var policy = await NoteRules.PolicyAsync(db, note.AgencyId, cancellationToken);
        var author = removed.Count > 0 ? await ParcelStatusChange.AuthorNameAsync(db, caller.UserId, cancellationToken) : string.Empty;
        var validated = links.Where(l => (kept is null || kept.Contains(l.ParcelId)) && InChunk(l)).ToList();
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            if (removed.Count > 0)
            {
                await NoteRules.RemoveFromNoteAsync(db, caller, note, removed.Select(l => l.Parcel).ToList(), openRouteIds, author, note.LocationCityId, now, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
            }
            if (note.Status != DeliveryNoteStatus.VALIDATED)
            {
                await NoteRules.ApplyValidationAsync(db, caller, note, validated.Select(l => l.Parcel).ToList(), policy, now, cancellationToken,
                    parcelsOnly: options.ParcelsPhase, noteOnly: options.FinishPhase, pickedUpCount: options.PickedUpCount);
            }
            if (openRouteIds.Count > 0 && !options.ParcelsPhase)
            {
                var ids = validated.Select(l => l.ParcelId).ToList();
                foreach (var link in await db.RouteParcels.Where(l => openRouteIds.Contains(l.RouteId) && ids.Contains(l.ParcelId) && !l.Confirmed).ToListAsync(cancellationToken))
                {
                    link.Confirmed = true;
                    link.ConfirmedAt = now;
                    link.ConfirmedBy = caller.UserId;
                }
            }
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        if (!options.ParcelsPhase)
        {
            var announced = options.FinishPhase ? options.RemovedTotal ?? 0 : removed.Count;
            if (announced > 0) await AnnounceRemovalAsync(db, logger, note, announced, cancellationToken);
        }
        return new ChunkResult(removed.Count, validated.Count);
    }

    /// <summary>`announceRemoval` : le vendeur apprend combien de colis n'ont pas été remis.</summary>
    private static async Task AnnounceRemovalAsync(IAppDbContext db, ILogger logger, DeliveryNote note, int removed, CancellationToken cancellationToken)
    {
        try
        {
            var kept = await db.DeliveryNoteParcels.CountAsync(l => l.DeliveryNoteId == note.Id, cancellationToken);
            var removedTotal = await db.DeliveryNoteRemovedParcels.CountAsync(r => r.DeliveryNoteId == note.Id, cancellationToken);
            await Notifier.NotifyMerchantAsync(db, note.ClientId, "note-parcels-removed",
                new Dictionary<string, object> { ["removed"] = removed, ["picked"] = kept, ["total"] = kept + removedTotal, ["code"] = note.Code },
                new Dictionary<string, string> { ["noteCode"] = note.Code }, cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning("Notification « note-parcels-removed » du bon {Code} non envoyée : {Message}", note.Code, error.Message);
        }
    }
}

internal sealed class ValidateNoteHandler(IAppDbContext db, ICurrentUser caller, ILogger<ValidateNoteHandler> logger, BulkJobs.BulkJobQueue jobs) : IRequestHandler<ValidateNoteCommand, object>
{
    public async Task<object> Handle(ValidateNoteCommand input, CancellationToken cancellationToken)
    {
        var note = await NoteRows.Scoped(db.DeliveryNotes.AsNoTracking(), caller).Where(n => n.Code == input.Code)
            .Select(n => new { n.Id, n.Code, n.Status, Parcels = n.Parcels.Select(l => new { l.ParcelId, l.Parcel.StatusId }).ToList() }).FirstOrDefaultAsync(cancellationToken)
            ?? throw BusinessException.NotFound($"Bon introuvable : {input.Code}");
        if (input.Async && note.Status != DeliveryNoteStatus.VALIDATED && note.Parcels.Count > NoteRules.BulkAsyncThreshold)
        {
            var kept = input.ParcelIds?.ToHashSet();
            var picked = note.Parcels.Count(p => (kept is null || kept.Contains(p.ParcelId)) && (p.StatusId == NoteRules.PickedUp || NoteRules.PickupSourceStatusIds.Contains(p.StatusId)));
            var job = await BulkJobs.BulkJobWrites.CreateAsync(db, caller, jobs, BulkJobs.BulkJobKinds.NoteValidate, note.Code,
                new Dictionary<string, object?> { ["kept"] = input.ParcelIds, ["pickedUpCount"] = picked }, note.Parcels.Select(p => p.ParcelId).ToList(), cancellationToken);
            return new BulkJobs.BulkJobAcceptedDto(job);
        }
        await BulkJobs.BulkJobWrites.AssertIdleAsync(db, note.Code, cancellationToken);
        await NoteValidation.RunAsync(db, caller, logger, input.Code, input.ParcelIds, new NoteValidation.Options(), cancellationToken);
        return await NoteRows.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}

/// <summary>`POST /merchant/me/pickups` et `/dropoffs` (`pickupRequestSchema`) : le vendeur ouvre son bon.</summary>
public sealed record RequestMerchantNoteCommand : ParcelSelection, IRequest<DeliveryNoteDetailDto>
{
    [JsonIgnore]
    public DeliveryNoteType Kind { get; init; } = DeliveryNoteType.PICKUP;
}

public sealed class RequestMerchantNoteValidator : ParcelSelectionValidator<RequestMerchantNoteCommand>;

internal sealed class RequestMerchantNoteHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<RequestMerchantNoteCommand, DeliveryNoteDetailDto>
{
    public async Task<DeliveryNoteDetailDto> Handle(RequestMerchantNoteCommand input, CancellationToken cancellationToken)
    {
        var clientId = caller.ClientId ?? throw BusinessException.Forbidden("Cet espace est réservé aux e-commerçants.", new { reason = "not-a-merchant" });
        var ids = await NoteRules.ResolveSelectionAsync(input.ParcelIds, input.All, input.Exclude, take => NoteRules.CandidateIdsAsync(db, clientId, input.Search, take, cancellationToken));
        var client = await db.Clients.AsNoTracking().Where(c => c.Id == clientId).Select(c => new { c.AgencyId, c.CityId }).FirstOrDefaultAsync(cancellationToken)
                     ?? throw BusinessException.NotFound("Fiche client introuvable.");
        var agencyId = client.AgencyId ?? await db.Agencies.AsNoTracking().Where(a => a.DeletedAt == null).OrderBy(a => a.CreatedAt).Select(a => a.Id).FirstOrDefaultAsync(cancellationToken)
                       ?? throw BusinessException.Validation("Aucune agence disponible.");
        var candidates = await db.Parcels.AsNoTracking()
            .Where(p => p.ClientId == clientId && ids.Contains(p.Id) && NoteRules.CandidateStatusIds.Contains(p.StatusId) && p.DeletedAt == null).Select(p => p.Id).ToListAsync(cancellationToken);
        if (candidates.Count == 0)
        {
            var pickup = input.Kind == DeliveryNoteType.PICKUP;
            throw BusinessException.Validation(pickup ? "Aucun colis à faire ramasser dans cette sélection." : "Aucun colis à déposer dans cette sélection.",
                new { reason = pickup ? "no-pickup-candidate" : "no-dropoff-candidate" });
        }
        var code = await NoteCreation.CreateAsync(db, caller, input.Kind, clientId, agencyId, client.CityId, candidates, cancellationToken);
        return await NoteRows.DetailAsync(db, caller, code, cancellationToken);
    }
}

/// <summary>`DELETE /merchant/me/notes/:code` : le vendeur annule son bon (mêmes gardes que la suppression).</summary>
public sealed record CancelMerchantNoteCommand(string Code) : IRequest<NoteCodeDto>;

internal sealed class CancelMerchantNoteHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<CancelMerchantNoteCommand, NoteCodeDto>
{
    public Task<NoteCodeDto> Handle(CancelMerchantNoteCommand request, CancellationToken cancellationToken)
    {
        if (caller.ClientId is null) throw BusinessException.Forbidden("Cet espace est réservé aux e-commerçants.", new { reason = "not-a-merchant" });
        return NoteDeletion.DeleteAsync(db, caller, request.Code, cancellationToken);
    }
}
