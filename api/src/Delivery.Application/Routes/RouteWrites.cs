using Delivery.Application.Common.Interfaces;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.Parcels.Commands;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using Delivery.Domain.Parcels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Routes;

/// <summary>`RouteCandidate` : un colis que le circuit peut prendre.</summary>
public sealed record RouteCandidateDto(string Id, string Code, string RecipientName, string RecipientPhone, string? DestinationDistrictId, string? DestinationDistrictName,
    string DeliveryAddress, int StatusId, long CodToCollect, string ClientName, string SenderPhone, string PickupAddress, string OriginCityName, string CityId, string CityName,
    CandidateHolderDto? HeldBy);

public sealed record CandidateHolderDto(string? NoteCode, string? RouteCode, bool Locked);

public sealed record RouteNoteCandidateDto(string Id, string Code, string ClientName, string ClientPhone, string PickupAddress, string CityName, int ParcelCount, DateTime CreatedAt);

public sealed record CandidatePageDto<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize, int SelectableTotal);

public sealed record NamedKeyDto(string Key, string Name);

public sealed record NamedIdDto(string Id, string Name);

/// <summary>`routeCandidatePage` : une page, et les quartiers et villes représentés.</summary>
public sealed record RouteCandidatePageDto(IReadOnlyList<RouteCandidateDto> Items, int Total, int Page, int PageSize, int SelectableTotal, IReadOnlyList<NamedKeyDto> Districts, IReadOnlyList<NamedIdDto> Cities);

/// <summary>Les écritures des circuits en brouillon (`RoutesService`).</summary>
public static class RouteWrites
{
    public const int CandidatePageSize = 100;

    public static void AssertDraft(RouteStatus status)
    {
        if (status != RouteStatus.DRAFT) throw BusinessException.Conflict("Ce circuit est parti : sa composition ne change plus.", new { reason = "route-not-draft" });
    }

    /// <summary>`originCityOf` : la ville de l'agence de l'agent, sinon celle du circuit.</summary>
    public static async Task<string> OriginCityOfAsync(IAppDbContext db, ICurrentUser caller, string fallback, CancellationToken cancellationToken) =>
        caller.AgencyId is null ? fallback : await db.Agencies.AsNoTracking().Where(a => a.Id == caller.AgencyId).Select(a => a.CityId).FirstOrDefaultAsync(cancellationToken) ?? fallback;

    /// <summary>`routeCityIds` : les villes de la grille du livreur, sinon celle du circuit.</summary>
    public static async Task<List<string>> CityIdsAsync(IAppDbContext db, Route route, CancellationToken cancellationToken)
    {
        var grid = await db.CourierCities.AsNoTracking().Where(c => c.CourierId == route.CourierId).Select(c => c.CityId).ToListAsync(cancellationToken);
        return grid.Count > 0 ? grid : [route.CityId];
    }

    public static RouteKind KindOf(string? kind) => kind switch { "pickup" => RouteKind.PICKUP, "return" => RouteKind.RETURN, _ => RouteKind.DELIVERY };

    public static async Task NotifyWithdrawnAsync(IAppDbContext db, ILogger logger, string courierId, string routeCode, CancellationToken cancellationToken)
    {
        try
        {
            await Notifier.NotifyCourierAsync(db, courierId, "route-withdrawn", new Dictionary<string, object> { ["code"] = routeCode },
                new Dictionary<string, string> { ["routeCode"] = routeCode }, cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning("Notification « route-withdrawn » du circuit {Code} non envoyée : {Message}", routeCode, error.Message);
        }
    }

    /// <summary>`routeCandidates` : même ville, prêts à partir, libres de tout autre circuit (ramassage : en attente, prise possible sur un brouillon).</summary>
    public static async Task<List<RouteCandidateDto>> CandidatesAsync(IAppDbContext db, ICurrentUser caller, string code, string? search, CancellationToken cancellationToken)
    {
        var route = await RouteRows.FindAsync(db, caller, code, cancellationToken);
        var cities = await CityIdsAsync(db, route, cancellationToken);
        var parcels = db.Parcels.AsNoTracking().Where(p => p.DeletedAt == null);
        var pickup = route.Kind == RouteKind.PICKUP;
        var toSender = StatusCatalog.RouteTargetsSender(Money.ToWire(route.Kind));
        if (pickup)
        {
            var routeId = route.Id;
            parcels = parcels.Where(p => cities.Contains(p.OriginCityId) && NoteRules.CandidateStatusIds.Contains(p.StatusId) && !p.RouteLinks.Any(l => l.RouteId == routeId));
        }
        else
        {
            var eligible = StatusCatalog.RouteEligibleStatusIds(Money.ToWire(route.Kind));
            parcels = toSender ? parcels.Where(p => cities.Contains(p.OriginCityId)) : parcels.Where(p => cities.Contains(p.DestinationCityId));
            parcels = parcels.Where(p => eligible.Contains(p.StatusId) && !p.RouteLinks.Any(l => l.Route.Status != RouteStatus.CLOSED));
        }
        if (!string.IsNullOrWhiteSpace(search)) parcels = ParcelFilters.Search(parcels, search);
        var rows = await parcels.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Code).Take(NoteRules.SelectionMax + 1)
            .Select(p => new
            {
                p.Id, p.Code, p.RecipientName, p.RecipientPhone, p.DestinationDistrictId, p.DestinationDistrictName,
                DistrictRef = p.DestinationDistrict != null ? p.DestinationDistrict.Name : null, p.DeliveryAddress, p.StatusId, p.CodAmount, p.DeliveryFee, p.FeeIncluded, p.AlreadyPaid,
                ClientName = p.Client.Name, SenderPhone = p.Client.Phone, p.PickupAddress, OriginName = p.OriginCity.Name, p.OriginCityId, p.DestinationCityId, DestinationName = p.DestinationCity.Name,
                OnRoute = p.RouteLinks.Where(l => l.Route.Status != RouteStatus.CLOSED).Select(l => new { l.Route.Code, l.Route.Status }).FirstOrDefault(),
                OnNote = p.NoteLinks.Where(l => NoteRules.ActiveStatuses.Contains(l.DeliveryNote.Status)).Select(l => l.DeliveryNote.Code).FirstOrDefault(),
            }).ToListAsync(cancellationToken);
        return rows.Select(p => new RouteCandidateDto(p.Id, p.Code, p.RecipientName, p.RecipientPhone, p.DestinationDistrictId, ParcelViews.DistrictLabel(p.DestinationDistrictName, p.DistrictRef),
            p.DeliveryAddress, p.StatusId, Billing.CodToCollect(Money.ToCents(p.CodAmount), Money.ToCents(p.DeliveryFee), p.FeeIncluded, p.AlreadyPaid), p.ClientName, p.SenderPhone,
            p.PickupAddress, p.OriginName, pickup || toSender ? p.OriginCityId : p.DestinationCityId, pickup || toSender ? p.OriginName : p.DestinationName,
            pickup && (p.OnRoute is not null || p.OnNote is not null) ? new CandidateHolderDto(p.OnNote, p.OnRoute?.Code, p.OnRoute is not null && p.OnRoute.Status != RouteStatus.DRAFT) : null)).ToList();
    }

    public static IEnumerable<RouteCandidateDto> InCity(IEnumerable<RouteCandidateDto> rows, string? cityId) => string.IsNullOrEmpty(cityId) ? rows : rows.Where(r => r.CityId == cityId);

    public static IEnumerable<RouteCandidateDto> InDistrict(IEnumerable<RouteCandidateDto> rows, string? district) =>
        string.IsNullOrEmpty(district) ? rows : rows.Where(r => CityNames.DistrictKey(r.DestinationDistrictName ?? string.Empty) == district);

    /// <summary>`pageOf` : page ≥ 1, taille 100 par défaut, 200 au plus.</summary>
    public static (int Page, int PageSize) PageOf(string? page, string? pageSize)
    {
        static int Number(string? value, int fallback) => double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n) && n != 0 && double.IsFinite(n) ? (int)n : fallback;
        return (Math.Max(Number(page, 1), 1), Math.Min(Math.Max(Number(pageSize, CandidatePageSize), 1), 200));
    }

    public static CandidatePageDto<T> Page<T>(IReadOnlyList<T> rows, string? page, string? pageSize, Func<T, bool>? locked = null)
    {
        var (p, size) = PageOf(page, pageSize);
        return new CandidatePageDto<T>(rows.Skip((p - 1) * size).Take(size).ToList(), rows.Count, p, size, rows.Count(r => locked is null || !locked(r)));
    }

    /// <summary>`routeNoteCandidates` : bons de ramassage en attente de la ville, libres de tout autre circuit, non vides.</summary>
    public static async Task<List<RouteNoteCandidateDto>> NoteCandidatesAsync(IAppDbContext db, ICurrentUser caller, string code, string? search, CancellationToken cancellationToken)
    {
        var route = await RouteRows.FindAsync(db, caller, code, cancellationToken);
        if (route.Kind != RouteKind.PICKUP) return [];
        var cities = await CityIdsAsync(db, route, cancellationToken);
        var notes = db.DeliveryNotes.AsNoTracking().Where(n => n.Type == DeliveryNoteType.PICKUP && n.Status == DeliveryNoteStatus.AWAITING_VALIDATION && cities.Contains(n.LocationCityId)
                                                             && !n.Routes.Any(l => l.Route.Status != RouteStatus.CLOSED) && n.Parcels.Any());
        if (!string.IsNullOrEmpty(search))
        {
            var pattern = Common.Models.Like.Contains(search);
            notes = notes.Where(n => EF.Functions.ILike(n.Code, pattern) || EF.Functions.ILike(n.Client.Name, pattern));
        }
        return await notes.OrderByDescending(n => n.CreatedAt).ThenBy(n => n.Code).Take(NoteRules.SelectionMax + 1)
            .Select(n => new RouteNoteCandidateDto(n.Id, n.Code, n.Client.Name, n.Client.Phone, n.Client.Address ?? string.Empty, n.LocationCity.Name, n.Parcels.Count, n.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    /// <summary>`addRouteParcels` / `addPickupRouteParcels`.</summary>
    public static async Task AddParcelsAsync(IAppDbContext db, ICurrentUser caller, Route route, IReadOnlyList<string> parcelIds, CancellationToken cancellationToken)
    {
        var parcels = await db.Parcels.Where(p => parcelIds.Contains(p.Id) && p.DeletedAt == null).ToListAsync(cancellationToken);
        if (parcels.Count != parcelIds.Count) throw BusinessException.Validation("Colis inconnu.", new { field = "parcelIds" });
        var cities = await CityIdsAsync(db, route, cancellationToken);
        var existing = (await db.RouteParcels.Where(l => l.RouteId == route.Id).Select(l => l.ParcelId).ToListAsync(cancellationToken)).ToHashSet();
        if (route.Kind == RouteKind.PICKUP)
        {
            await AddPickupParcelsAsync(db, caller, route, parcels, cities, existing, cancellationToken);
            return;
        }
        var kind = Money.ToWire(route.Kind);
        var toSender = StatusCatalog.RouteTargetsSender(kind);
        foreach (var parcel in parcels)
        {
            if (!cities.Contains(toSender ? parcel.OriginCityId : parcel.DestinationCityId))
            {
                throw BusinessException.Conflict($"Le colis {parcel.Code} ne va pas dans la ville de ce circuit.", new { reason = "parcel-other-city", code = parcel.Code });
            }
            if (!StatusCatalog.IsRouteEligible(kind, parcel.StatusId))
            {
                throw BusinessException.Conflict($"Le colis {parcel.Code} n'est pas prêt à partir.", new { reason = "parcel-not-ready", code = parcel.Code, statusId = parcel.StatusId });
            }
        }
        var busy = await db.RouteParcels.AsNoTracking().Where(l => parcelIds.Contains(l.ParcelId) && l.RouteId != route.Id && l.Route.Status != RouteStatus.CLOSED)
            .Select(l => new { Parcel = l.Parcel.Code, Route = l.Route.Code }).FirstOrDefaultAsync(cancellationToken);
        if (busy is not null) throw BusinessException.Conflict($"Le colis {busy.Parcel} est déjà dans le circuit {busy.Route}.", new { reason = "parcel-in-route", code = busy.Parcel });
        foreach (var parcel in parcels.Where(p => !existing.Contains(p.Id))) db.RouteParcels.Add(new RouteParcel { RouteId = route.Id, ParcelId = parcel.Id });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task AddPickupParcelsAsync(IAppDbContext db, ICurrentUser caller, Route route, List<Parcel> parcels, List<string> cities, HashSet<string> existing, CancellationToken cancellationToken)
    {
        foreach (var parcel in parcels)
        {
            if (!cities.Contains(parcel.OriginCityId)) throw BusinessException.Conflict($"Le colis {parcel.Code} ne va pas dans la ville de ce circuit.", new { reason = "parcel-other-city", code = parcel.Code });
            if (!NoteRules.CandidateStatusIds.Contains(parcel.StatusId))
            {
                throw BusinessException.Conflict($"Le colis {parcel.Code} n'est pas en attente de ramassage.", new { reason = "parcel-not-ready", code = parcel.Code, statusId = parcel.StatusId });
            }
        }
        var ids = parcels.Select(p => p.Id).ToList();
        var routeLinks = await db.RouteParcels.Where(l => ids.Contains(l.ParcelId) && l.RouteId != route.Id && l.Route.Status != RouteStatus.CLOSED).Include(l => l.Route).ToListAsync(cancellationToken);
        var noteLinks = await db.DeliveryNoteParcels.Where(l => ids.Contains(l.ParcelId) && l.DeliveryNote.Status == DeliveryNoteStatus.AWAITING_VALIDATION).Include(l => l.DeliveryNote).ToListAsync(cancellationToken);
        var locked = routeLinks.FirstOrDefault(l => l.Route.Status != RouteStatus.DRAFT);
        if (locked is not null)
        {
            var code = parcels.First(p => p.Id == locked.ParcelId).Code;
            throw BusinessException.Validation($"Le colis {code} est déjà sur le circuit dispatché {locked.Route.Code}.", new { reason = "parcel-in-dispatched-route", code, routeCode = locked.Route.Code });
        }
        var now = DateTime.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var parcel in parcels)
        {
            var fromRoutes = routeLinks.Where(l => l.ParcelId == parcel.Id).ToList();
            var fromNotes = noteLinks.Where(l => l.ParcelId == parcel.Id).ToList();
            db.RouteParcels.RemoveRange(fromRoutes);
            db.DeliveryNoteParcels.RemoveRange(fromNotes);
            var from = fromRoutes.Select(l => l.Route.Code).Concat(fromNotes.Select(l => l.DeliveryNote.Code)).ToList();
            var target = parcel.StatusId == NoteRules.AwaitingArrival ? NoteRules.AwaitingPickup : parcel.StatusId;
            var changed = target != parcel.StatusId;
            if (changed)
            {
                parcel.StatusId = target;
                parcel.StatusAt = now;
            }
            if (from.Count > 0 || changed)
            {
                db.ParcelEvents.Add(new ParcelEvent
                {
                    ParcelId = parcel.Id, StatusId = target, AuthorId = caller.UserId, CityId = route.CityId, CreatedAt = now,
                    Comment = from.Count > 0 ? $"Déplacé de {string.Join(" / ", from)} vers {route.Code}" : $"Mis sur le circuit de ramassage {route.Code} : le colis sera ramassé.",
                });
            }
            if (!existing.Contains(parcel.Id)) db.RouteParcels.Add(new RouteParcel { RouteId = route.Id, ParcelId = parcel.Id });
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>`addRouteNotes` : des bons de ramassage de la ville, en attente, et tous leurs colis.</summary>
    public static async Task AddNotesAsync(IAppDbContext db, Route route, IReadOnlyList<string> noteIds, CancellationToken cancellationToken)
    {
        var notes = await db.DeliveryNotes.AsNoTracking().Where(n => noteIds.Contains(n.Id))
            .Select(n => new { n.Id, n.Code, n.Type, n.Status, n.LocationCityId, Parcels = n.Parcels.Select(l => new { l.ParcelId, l.Parcel.Code, l.Parcel.StatusId }).ToList() })
            .ToListAsync(cancellationToken);
        if (notes.Count != noteIds.Count) throw BusinessException.Validation("Bon inconnu.", new { field = "noteIds" });
        var cities = await CityIdsAsync(db, route, cancellationToken);
        var kind = Money.ToWire(route.Kind);
        foreach (var note in notes)
        {
            if (note.Type != DeliveryNoteType.PICKUP) throw BusinessException.Conflict($"Le bon {note.Code} n'est pas un bon de ramassage.", new { reason = "note-not-pickup", code = note.Code });
            if (note.Status != DeliveryNoteStatus.AWAITING_VALIDATION) throw BusinessException.Conflict($"Le bon {note.Code} n'est plus en attente.", new { reason = "note-not-awaiting", code = note.Code });
            if (!cities.Contains(note.LocationCityId)) throw BusinessException.Conflict($"Le bon {note.Code} n'est pas dans la ville de ce circuit.", new { reason = "note-other-city", code = note.Code });
            if (note.Parcels.Count == 0) throw BusinessException.Conflict($"Le bon {note.Code} ne porte aucun colis.", new { reason = "note-empty", code = note.Code });
            foreach (var parcel in note.Parcels.Where(p => !StatusCatalog.IsRouteEligible(kind, p.StatusId)))
            {
                throw BusinessException.Conflict($"Le colis {parcel.Code} n'est pas prêt à partir.", new { reason = "parcel-not-ready", code = parcel.Code, statusId = parcel.StatusId });
            }
        }
        var busyNote = await db.RouteNotes.AsNoTracking().Where(l => noteIds.Contains(l.DeliveryNoteId) && l.RouteId != route.Id && l.Route.Status != RouteStatus.CLOSED)
            .Select(l => new { Note = l.DeliveryNote.Code, Route = l.Route.Code }).FirstOrDefaultAsync(cancellationToken);
        if (busyNote is not null) throw BusinessException.Conflict($"Le bon {busyNote.Note} est déjà dans le circuit {busyNote.Route}.", new { reason = "note-in-route", code = busyNote.Note });
        var parcelIds = notes.SelectMany(n => n.Parcels.Select(p => p.ParcelId)).Distinct().ToList();
        var busyParcel = await db.RouteParcels.AsNoTracking().Where(l => parcelIds.Contains(l.ParcelId) && l.RouteId != route.Id && l.Route.Status != RouteStatus.CLOSED)
            .Select(l => new { Parcel = l.Parcel.Code, Route = l.Route.Code }).FirstOrDefaultAsync(cancellationToken);
        if (busyParcel is not null) throw BusinessException.Conflict($"Le colis {busyParcel.Parcel} est déjà dans le circuit {busyParcel.Route}.", new { reason = "parcel-in-route", code = busyParcel.Parcel });
        var knownNotes = (await db.RouteNotes.Where(l => l.RouteId == route.Id).Select(l => l.DeliveryNoteId).ToListAsync(cancellationToken)).ToHashSet();
        var knownParcels = (await db.RouteParcels.Where(l => l.RouteId == route.Id).Select(l => l.ParcelId).ToListAsync(cancellationToken)).ToHashSet();
        foreach (var note in notes.Where(n => !knownNotes.Contains(n.Id))) db.RouteNotes.Add(new RouteNote { RouteId = route.Id, DeliveryNoteId = note.Id });
        foreach (var id in parcelIds.Where(id => !knownParcels.Contains(id))) db.RouteParcels.Add(new RouteParcel { RouteId = route.Id, ParcelId = id });
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>`removeRouteNote` : le bon quitte le circuit avec ses colis qu'aucun autre bon du circuit ne porte.</summary>
    public static async Task RemoveNoteAsync(IAppDbContext db, Route route, string noteId, CancellationToken cancellationToken)
    {
        var mine = await db.DeliveryNoteParcels.Where(l => l.DeliveryNoteId == noteId).Select(l => l.ParcelId).ToListAsync(cancellationToken);
        var others = (await db.DeliveryNoteParcels.Where(l => l.DeliveryNoteId != noteId && l.DeliveryNote.Routes.Any(r => r.RouteId == route.Id)).Select(l => l.ParcelId).ToListAsync(cancellationToken)).ToHashSet();
        var drop = mine.Where(id => !others.Contains(id)).ToList();
        db.RouteNotes.RemoveRange(await db.RouteNotes.Where(l => l.RouteId == route.Id && l.DeliveryNoteId == noteId).ToListAsync(cancellationToken));
        if (drop.Count > 0) db.RouteParcels.RemoveRange(await db.RouteParcels.Where(l => l.RouteId == route.Id && drop.Contains(l.ParcelId)).ToListAsync(cancellationToken));
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>`pickupRouteNote` : le livreur prend les colis du bon ; les non remis peuvent en sortir.</summary>
    public static async Task PickupNoteAsync(IAppDbContext db, ICurrentUser caller, ILogger logger, Route route, string noteId, IReadOnlyList<string> parcelIds, bool removeUnpicked, CancellationToken cancellationToken)
    {
        if (route.Kind != RouteKind.PICKUP) throw BusinessException.Conflict("Seul un circuit de ramassage porte des bons.", new { reason = "route-not-pickup" });
        if (route.Status == RouteStatus.DRAFT) throw BusinessException.Conflict("Ce circuit n’est pas encore parti.", new { reason = "route-not-dispatched" });
        if (!await db.RouteNotes.AnyAsync(l => l.RouteId == route.Id && l.DeliveryNoteId == noteId, cancellationToken)) throw BusinessException.NotFound("Ce bon n’est pas sur ce circuit.");
        var note = await db.DeliveryNotes.FirstAsync(n => n.Id == noteId, cancellationToken);
        if (note.Status == DeliveryNoteStatus.VALIDATED) throw BusinessException.Conflict("Ce bon est déjà réceptionné à l’entrepôt.", new { reason = "note-validated" });
        var links = await db.DeliveryNoteParcels.Where(l => l.DeliveryNoteId == note.Id).Include(l => l.Parcel).ToListAsync(cancellationToken);
        var belongs = links.ToDictionary(l => l.ParcelId, l => l.Parcel);
        var wanted = parcelIds.Distinct().ToList();
        var foreign = wanted.Where(id => !belongs.ContainsKey(id)).ToList();
        if (foreign.Count > 0)
        {
            var stray = await db.Parcels.AsNoTracking().Where(p => foreign.Contains(p.Id)).Select(p => p.Code).FirstOrDefaultAsync(cancellationToken);
            throw BusinessException.Conflict(stray is not null ? $"Le colis {stray} n'appartient pas au bon {note.Code}." : $"Un colis scanné n'appartient pas au bon {note.Code}.",
                new { reason = "parcel-not-in-note", code = stray, noteCode = note.Code });
        }
        var now = DateTime.UtcNow;
        var picked = wanted.ToHashSet();
        var unpicked = removeUnpicked ? links.Select(l => l.Parcel).Where(p => !picked.Contains(p.Id) && p.StatusId is NoteRules.AwaitingPickup or NoteRules.PickupInProgress).ToList() : [];
        var openRoutes = unpicked.Count > 0 ? await NoteRules.OpenRoutesOfNoteAsync(db, note.Id, cancellationToken) : [];
        var author = unpicked.Count > 0 ? await ParcelStatusChange.AuthorNameAsync(db, caller.UserId, cancellationToken) : string.Empty;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            if (unpicked.Count > 0) await NoteRules.RemoveFromNoteAsync(db, caller, note, unpicked, openRoutes, author, route.CityId, now, cancellationToken);
            foreach (var id in wanted)
            {
                var parcel = belongs[id];
                if (parcel.StatusId == NoteRules.PickedUpByCourier) continue;
                if (!StatusCatalog.CanTransition(parcel.StatusId, NoteRules.PickedUpByCourier))
                {
                    throw BusinessException.Transition($"Le colis {parcel.Code} ne peut pas passer en « ramassé par le livreur ».",
                        new { reason = "parcel-not-pickable", from = parcel.StatusId, allowed = StatusCatalog.AllowedTransitions(parcel.StatusId) });
                }
                db.ParcelEvents.Add(new ParcelEvent { ParcelId = id, StatusId = NoteRules.PickedUpByCourier, Comment = $"Bon {note.Code} — circuit {route.Code}", AuthorId = caller.UserId, CityId = route.CityId, CreatedAt = now });
                parcel.StatusId = NoteRules.PickedUpByCourier;
                parcel.StatusAt = now;
                foreach (var link in await db.RouteParcels.Where(l => l.RouteId == route.Id && l.ParcelId == id).ToListAsync(cancellationToken))
                {
                    link.Confirmed = true;
                    link.ConfirmedAt = now;
                    link.ConfirmedBy = caller.UserId;
                }
            }
            note.Status = DeliveryNoteStatus.PICKED_UP_BY_COURIER;
            note.PickedUpAt = now;
            note.PickedUpBy = caller.UserId;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        if (unpicked.Count > 0)
        {
            try
            {
                var kept = await db.DeliveryNoteParcels.CountAsync(l => l.DeliveryNoteId == note.Id, cancellationToken);
                var removedTotal = await db.DeliveryNoteRemovedParcels.CountAsync(r => r.DeliveryNoteId == note.Id, cancellationToken);
                await Notifier.NotifyMerchantAsync(db, note.ClientId, "note-parcels-removed",
                    new Dictionary<string, object> { ["removed"] = unpicked.Count, ["picked"] = kept, ["total"] = kept + removedTotal, ["code"] = note.Code },
                    new Dictionary<string, string> { ["noteCode"] = note.Code }, cancellationToken);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                logger.LogWarning("Notification « note-parcels-removed » du bon {Code} non envoyée : {Message}", note.Code, error.Message);
            }
        }
    }

    /// <summary>`removeParcelFromDraft` : le colis quitte le brouillon, sauf s'il est déjà en main du livreur.</summary>
    public static async Task RemoveParcelFromDraftAsync(IAppDbContext db, ICurrentUser caller, Route route, string parcelId, string gate, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var link = await db.RouteParcels.Include(l => l.Parcel).FirstOrDefaultAsync(l => l.RouteId == route.Id && l.ParcelId == parcelId, cancellationToken);
        if (link is null) return;
        var parcel = link.Parcel;
        if (!StatusCatalog.IsRouteEligible(Money.ToWire(route.Kind), parcel.StatusId) && parcel.CourierId == route.CourierId)
        {
            throw BusinessException.Conflict($"Le colis {parcel.Code} est déjà en main, transféré en cours de livraison : il ne se retire pas de la tournée. Transférez-le à un autre livreur ou rapportez son résultat.",
                new { reason = "parcel-in-hand", code = parcel.Code, statusId = parcel.StatusId });
        }
        db.RouteParcels.Remove(link);
        var author = await ParcelStatusChange.AuthorNameAsync(db, caller.UserId, cancellationToken);
        var summary = $"Retiré du circuit {route.Code} (brouillon) — {(gate == "courier" ? "téléphone du livreur" : "bureau")}";
        db.ParcelEvents.Add(new ParcelEvent { ParcelId = parcelId, StatusId = parcel.StatusId, Comment = summary, AuthorId = caller.UserId, AuthorName = author, CityId = route.CityId, CreatedAt = DateTime.UtcNow });
        db.AuditLogs.Add(new AuditLog
        {
            Entity = "Parcel", EntityId = parcelId, Action = AuditAction.UPDATE, Before = System.Text.Json.JsonSerializer.Serialize(new { routeCode = route.Code }),
            After = System.Text.Json.JsonSerializer.Serialize(new { routeCode = (string?)null, removedFrom = route.Code, gate, summary }), Diff = ["routeCode"], UserId = caller.UserId, At = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
