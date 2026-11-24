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
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
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

/// <summary>`remittanceSummaryQuerySchema` : les cartes au-dessus de la liste, sur la période.</summary>
public sealed record GetRemittanceSummaryQuery(string? Period, string? From, string? To, IReadOnlyList<string>? CourierId, IReadOnlyList<string>? AgencyId) : IRequest<RemittanceSummaryDto>;

public sealed class GetRemittanceSummaryValidator : ZodValidator<GetRemittanceSummaryQuery>
{
    public GetRemittanceSummaryValidator()
    {
        RuleFor(x => x.Period).OneOf(Periods.All);
    }
}

internal sealed class GetRemittanceSummaryHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<GetRemittanceSummaryQuery, RemittanceSummaryDto>
{
    public async Task<RemittanceSummaryDto> Handle(GetRemittanceSummaryQuery request, CancellationToken cancellationToken)
    {
        var courierIds = caller.CourierId is { } own ? [own] : request.CourierId;
        var agencyIds = caller.IsSuperAdmin() ? request.AgencyId : null;
        var scoped = Remittances.Scoped(db.CashRemittances.AsNoTracking(), caller);
        if (courierIds is not null) scoped = scoped.Where(r => courierIds.Contains(r.CourierId));
        if (agencyIds is not null) scoped = scoped.Where(r => agencyIds.Contains(r.AgencyId));
        var timeZone = await db.Companies.AsNoTracking().OrderBy(c => c.CreatedAt).Select(c => c.Timezone).FirstOrDefaultAsync(cancellationToken);
        var first = await scoped.OrderBy(r => r.CreatedAt).Select(r => (DateTime?)r.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        var window = Periods.Resolve(request.Period ?? "month", request.From, request.To, Periods.Zone(string.IsNullOrEmpty(timeZone) ? "Africa/Casablanca" : timeZone), first, clock.UtcNow);
        var rows = await scoped.Where(r => r.CreatedAt >= window.Start && r.CreatedAt < window.End && r.Status != CashRemittanceStatus.CANCELLED)
            .Select(r => new { r.Status, r.ExpectedCod, r.TotalCash, r.TotalCheque, r.ExpectedNet, r.DeclaredAmount, r.Variance }).ToListAsync(cancellationToken);
        var open = await scoped.Where(r => r.Status == CashRemittanceStatus.DRAFT).OrderBy(r => r.CreatedAt)
            .Select(r => new RemittanceSummaryOpenDto(r.Id, r.Code, r.CourierId, r.Courier.FirstName + " " + r.Courier.LastName, r.Agency.Name, (long)Math.Round(r.ExpectedNet * 100m),
                (long)Math.Round(r.TotalCheque * 100m), r.Cheques.Count, r.Parcels.Count, r.CreatedAt, r.CourierClosedAt)).ToListAsync(cancellationToken);
        // `_sum` par statut, puis la somme des sommes : chaque statut arrondi au centime avant d'être additionné.
        var totals = new RemittanceSummaryTotalsDto(
            rows.GroupBy(r => r.Status).Sum(g => Money.ToCents(g.Sum(r => r.ExpectedCod))),
            rows.GroupBy(r => r.Status).Sum(g => Money.ToCents(g.Sum(r => r.TotalCash))),
            rows.GroupBy(r => r.Status).Sum(g => Money.ToCents(g.Sum(r => r.TotalCheque))),
            rows.GroupBy(r => r.Status).Sum(g => Money.ToCents(g.Sum(r => r.ExpectedNet))),
            rows.GroupBy(r => r.Status).Sum(g => Money.ToCents(g.Sum(r => r.DeclaredAmount ?? 0m))),
            rows.GroupBy(r => r.Status).Sum(g => Money.ToCents(g.Sum(r => r.Variance))),
            rows.Count, rows.Count(r => r.Status == CashRemittanceStatus.DRAFT), rows.Count(r => r.Status == CashRemittanceStatus.CONFIRMED));
        return new RemittanceSummaryDto(window.Period, window.From, window.To, courierIds?.Count == 1 ? courierIds[0] : null, totals, open);
    }
}
