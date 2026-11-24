using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Idempotency;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Application.Routes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using Delivery.Domain.Parcels;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.CourierApp;

/// <summary>Où est ce colis pour ce livreur (`locate`) : sur une de ses tournées ouvertes, la plus récente.</summary>
public static class CourierParcels
{
    public sealed record Located(string ParcelId, string Code, int StatusId, string RouteCode, RouteKind RouteKind);

    public static async Task<Located> LocateAsync(IAppDbContext db, ICurrentUser caller, string code, CancellationToken cancellationToken)
    {
        var courierId = CourierRouteViews.CourierIdOf(caller);
        var parcels = db.Parcels.AsNoTracking().Where(p => p.DeletedAt == null);
        var link = await ParcelLookup.ResolveAsync(parcels, code, async matched =>
        {
            var ids = matched.Select(p => p.Id);
            return await db.RouteParcels.AsNoTracking().Where(l => ids.Contains(l.ParcelId) && l.Route.CourierId == courierId && l.Route.Status != RouteStatus.CLOSED)
                .OrderByDescending(l => l.RouteId).Select(l => new Located(l.ParcelId, l.Parcel.Code, l.Parcel.StatusId, l.Route.Code, l.Route.Kind)).FirstOrDefaultAsync(cancellationToken);
        });
        return link ?? throw BusinessException.NotFound($"Le colis {code} n’est pas sur une de vos tournées.", new { reason = "parcel-not-mine", code });
    }
    /// <summary>`CourierStatusService.report` : l'issue passe par le circuit du livreur (encaissement compris).</summary>
    public static async Task<CourierRouteDto> ReportAsync(IAppDbContext db, ICurrentUser caller, ILogger logger, string code, RouteOutcome outcome, CancellationToken cancellationToken)
    {
        var located = await LocateAsync(db, caller, code, cancellationToken);
        if (outcome.StatusId is not { } target) throw BusinessException.Validation("Aucune issue rapportée.");
        if (StatusCatalog.IsLocked(located.StatusId))
        {
            throw BusinessException.Transition("Ce colis est livré : seule l’agence peut revenir sur ce statut.", new { reason = "parcel-locked", from = located.StatusId, to = target, allowed = Array.Empty<int>() });
        }
        var kind = Money.ToWire(located.RouteKind);
        if (!StatusCatalog.CanCourierReport(located.StatusId, target, kind))
        {
            throw BusinessException.Transition($"Un livreur ne peut pas passer ce colis de {located.StatusId} à {target}.",
                new { reason = "courier-transition-forbidden", from = located.StatusId, to = target, allowed = StatusCatalog.CourierOutcomesFor(located.StatusId, kind) });
        }
        await RouteParcelConfirm.RunAsync(db, caller, logger, located.RouteCode, located.ParcelId, outcome with { Confirmed = true }, false, cancellationToken);
        if (target == 4 && outcome.BlCollected == true) await Bl.BlCollection.CollectOnDeliveryAsync(db, caller, logger, located.Code, outcome.BlReference, cancellationToken);
        return await CourierRouteViews.RouteAsync(db, caller, located.RouteCode, cancellationToken, includeClosed: true);
    }

    /// <summary>`outcomes` : les issues que le livreur peut poser (aucune sur un colis remis sur un bon de retour).</summary>
    public static async Task<IReadOnlyList<int>> OutcomesAsync(IAppDbContext db, ICurrentUser caller, string code, CancellationToken cancellationToken)
    {
        var located = await LocateAsync(db, caller, code, cancellationToken);
        if (located.RouteKind == RouteKind.DELIVERY && await Returns.HandoverRelease.HandedOverOfAsync(db, located.ParcelId, cancellationToken) is not null) return [];
        return StatusCatalog.CourierOutcomesFor(located.StatusId, Money.ToWire(located.RouteKind));
    }
}
