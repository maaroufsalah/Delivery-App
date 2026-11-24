using System.Text.Json.Serialization;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Staff;

/// <summary>`GET /staff/me/alerts` : colis en relance répétée, circuits ouverts depuis 48 h, bons, caisses, réclamations.</summary>
public sealed record GetStaffAlertsQuery(string? AgencyId) : IRequest<StaffAlertsDto>;

internal sealed class GetStaffAlertsHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<GetStaffAlertsQuery, StaffAlertsDto>
{
    public const int StaleRouteHours = 48;
    public const int RetryAlertThreshold = 2;
    private const int Sample = 5;

    public async Task<StaffAlertsDto> Handle(GetStaffAlertsQuery request, CancellationToken cancellationToken)
    {
        var agency = !string.IsNullOrEmpty(request.AgencyId) && (caller.IsSuperAdmin() || request.AgencyId == caller.AgencyId) ? request.AgencyId : caller.AgencyId;
        var granted = await db.EffectivePermissionsAsync(caller.UserId, caller.Roles, cancellationToken);
        bool Has(string permission) => caller.IsSuperAdmin() || granted.Contains(permission);
        var now = clock.UtcNow;
        var alerts = new StaffAlertsDto(await StuckAsync(agency, cancellationToken), await StaleAsync(agency, now, cancellationToken), agency);
        if (Has("notes.read")) alerts = alerts with { PendingNotes = await NotesAsync(agency, cancellationToken) };
        if (Has("finance.read"))
        {
            alerts = alerts with
            {
                PendingRemittances = await RemittancesAsync(agency, r => r.Status == CashRemittanceStatus.DRAFT, cancellationToken),
                CashVariances = await RemittancesAsync(agency, r => r.Status == CashRemittanceStatus.CONFIRMED && r.VarianceReason != null, cancellationToken),
            };
        }
        if (Has("claims.read")) alerts = alerts with { OpenClaims = await ClaimsAsync(now, cancellationToken) };
        return alerts;
    }

    private async Task<StaffAlertGroupDto<StuckParcelAlertDto>> StuckAsync(string? agency, CancellationToken cancellationToken)
    {
        var rows = await db.ParcelEvents.AsNoTracking().Where(e => e.Status.Group == ParcelStatusGroup.RETRY).GroupBy(e => e.ParcelId)
            .Select(g => new { ParcelId = g.Key, Count = g.Count() }).Where(g => g.Count > RetryAlertThreshold).ToListAsync(cancellationToken);
        if (rows.Count == 0) return new StaffAlertGroupDto<StuckParcelAlertDto>(0, []);
        var attempts = rows.ToDictionary(r => r.ParcelId, r => r.Count);
        var ids = attempts.Keys.ToList();
        var parcels = db.Parcels.AsNoTracking().Where(p => p.DeletedAt == null && ids.Contains(p.Id));
        if (agency is not null) parcels = parcels.Where(p => p.AgencyId == agency);
        var items = (await parcels.Select(p => new { p.Id, p.Code, p.RecipientName, p.StatusId }).ToListAsync(cancellationToken))
            .Select(p => new StuckParcelAlertDto(p.Code, p.RecipientName, attempts.GetValueOrDefault(p.Id), p.StatusId)).OrderByDescending(p => p.Attempts).ToList();
        return new StaffAlertGroupDto<StuckParcelAlertDto>(items.Count, items.Take(Sample).ToList());
    }

    private async Task<StaffAlertGroupDto<StaleRouteAlertDto>> StaleAsync(string? agency, DateTime now, CancellationToken cancellationToken)
    {
        var limit = now.AddHours(-StaleRouteHours);
        var routes = db.Routes.AsNoTracking().Where(r => r.Status != RouteStatus.CLOSED && r.CreatedAt < limit);
        if (agency is not null) routes = routes.Where(r => r.Courier.AgencyId == agency);
        var count = await routes.CountAsync(cancellationToken);
        var rows = await routes.OrderBy(r => r.CreatedAt).Take(Sample).Select(r => new { r.Code, r.Courier.FirstName, r.Courier.LastName, City = r.City.Name, r.CreatedAt }).ToListAsync(cancellationToken);
        return new StaffAlertGroupDto<StaleRouteAlertDto>(count,
            rows.Select(r => new StaleRouteAlertDto(r.Code, $"{r.FirstName} {r.LastName}".Trim(), r.City, (long)Math.Round((now - r.CreatedAt).TotalHours, MidpointRounding.AwayFromZero))).ToList());
    }

    private async Task<StaffAlertGroupDto<PendingNoteAlertDto>> NotesAsync(string? agency, CancellationToken cancellationToken)
    {
        var notes = db.DeliveryNotes.AsNoTracking().Where(n => n.Type == DeliveryNoteType.PICKUP && n.Status == DeliveryNoteStatus.AWAITING_VALIDATION);
        if (agency is not null) notes = notes.Where(n => n.AgencyId == agency);
        var count = await notes.CountAsync(cancellationToken);
        var rows = await notes.OrderBy(n => n.CreatedAt).Take(Sample)
            .Select(n => new PendingNoteAlertDto(n.Code, n.Client.Name, n.LocationCity.Name, n.Parcels.Count, n.CreatedAt)).ToListAsync(cancellationToken);
        return new StaffAlertGroupDto<PendingNoteAlertDto>(count, rows);
    }

    private async Task<StaffAlertGroupDto<RemittanceAlertDto>> RemittancesAsync(string? agency, System.Linq.Expressions.Expression<Func<CashRemittance, bool>> kind, CancellationToken cancellationToken)
    {
        var remittances = db.CashRemittances.AsNoTracking().Where(kind);
        if (agency is not null) remittances = remittances.Where(r => r.AgencyId == agency);
        var count = await remittances.CountAsync(cancellationToken);
        var rows = await remittances.OrderBy(r => r.CreatedAt).Take(Sample)
            .Select(r => new { r.Code, r.Courier.FirstName, r.Courier.LastName, City = r.City.Name, r.Amount, r.DeclaredAmount, r.VarianceReason, r.CreatedAt }).ToListAsync(cancellationToken);
        return new StaffAlertGroupDto<RemittanceAlertDto>(count, rows.Select(r => new RemittanceAlertDto(r.Code, $"{r.FirstName} {r.LastName}".Trim(), r.City, Money.ToCents(r.Amount),
            r.DeclaredAmount is null ? 0 : Money.ToCents(r.DeclaredAmount.Value) - Money.ToCents(r.Amount), r.VarianceReason, r.CreatedAt)).ToList());
    }

    private async Task<StaffAlertGroupDto<ClaimAlertDto>> ClaimsAsync(DateTime now, CancellationToken cancellationToken)
    {
        var claims = db.Claims.AsNoTracking().Where(c => c.Status == ClaimStatus.OPEN || c.Status == ClaimStatus.IN_PROGRESS);
        var count = await claims.CountAsync(cancellationToken);
        var rows = await claims.OrderBy(c => c.CreatedAt).Take(Sample)
            .Select(c => new ClaimAlertDto(c.Code, c.Subject, c.Client.Name, c.DueAt != null && c.DueAt < now, c.CreatedAt)).ToListAsync(cancellationToken);
        return new StaffAlertGroupDto<ClaimAlertDto>(count, rows);
    }
}
