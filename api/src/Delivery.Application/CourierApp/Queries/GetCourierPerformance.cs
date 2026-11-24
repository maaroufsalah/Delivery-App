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

/// <summary>`GET /courier/me/performance` (`period` : jour ou mois, à Casablanca).</summary>
public sealed record GetCourierPerformanceQuery(string? Period) : IRequest<CourierPerformanceDto>;

public sealed class GetCourierPerformanceValidator : ZodValidator<GetCourierPerformanceQuery>
{
    public GetCourierPerformanceValidator()
    {
        RuleFor(x => x.Period).OneOf(["day", "month"]);
    }
}

internal sealed class GetCourierPerformanceHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<GetCourierPerformanceQuery, CourierPerformanceDto>
{
    public async Task<CourierPerformanceDto> Handle(GetCourierPerformanceQuery request, CancellationToken cancellationToken)
    {
        var courierId = caller.CourierId ?? throw BusinessException.Forbidden("Ce compte n’est pas rattaché à une fiche livreur.");
        var period = request.Period ?? "day";
        var since = period == "day" ? clock.TodayStartUtc() : Periods.ZonedDayStart(Periods.FirstOfMonth(clock.Today()), ClockDays.Zone);
        var mine = db.Parcels.AsNoTracking().Where(p => p.CourierId == courierId && p.DeletedAt == null);
        var settled = await mine.Where(p => p.StatusAt >= since).Select(p => new { p.StatusId, p.CodAmount, p.DeliveryFee, p.FeeIncluded, p.AlreadyPaid }).ToListAsync(cancellationToken);
        var pending = await mine.GroupBy(p => p.StatusId).Select(g => new { StatusId = g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        var outstanding = await mine.Where(p => p.StatusId == CourierPay.Delivered && !p.ReimbursedByCourier)
            .Select(p => new { p.CodAmount, p.DeliveryFee, p.FeeIncluded, p.AlreadyPaid }).ToListAsync(cancellationToken);
        int By(Func<int, bool> predicate) => pending.Where(r => predicate(r.StatusId)).Sum(r => r.Count);
        // Bug Speedy corrigé : `collected()` ignorait « déjà payé » ; le dû vient de `codToCollect`, comme partout.
        long Due(decimal cod, decimal fee, bool included, bool paid) => Billing.CodToCollect(Money.ToCents(cod), Money.ToCents(fee), included, paid);
        var delivered = settled.Where(r => r.StatusId == CourierPay.Delivered).ToList();
        return new CourierPerformanceDto(period,
            By(id => id is NoteRules.AwaitingPickup or NoteRules.PickupInProgress),
            By(id => id is RouteStatusIds.OutForDelivery or DeliveryCancel.AwaitingDelivery),
            By(id => id == CourierPay.Delivered),
            By(id => StatusCatalog.GroupOf(id) == "return"),
            By(id => StatusCatalog.GroupOf(id) == "failure"),
            delivered.Sum(r => Due(r.CodAmount, r.DeliveryFee, r.FeeIncluded, r.AlreadyPaid)),
            outstanding.Sum(r => Due(r.CodAmount, r.DeliveryFee, r.FeeIncluded, r.AlreadyPaid)),
            new CourierDeliveryRateDto(delivered.Count, settled.Count, settled.Count == 0 ? 0 : (int)Math.Round(delivered.Count * 100.0 / settled.Count, MidpointRounding.AwayFromZero)));
    }
}
