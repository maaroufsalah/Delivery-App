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
    private const int Sample = 5;

    public async Task<StaffAlertsDto> Handle(GetStaffAlertsQuery request, CancellationToken cancellationToken)
    {
        var agency = !string.IsNullOrEmpty(request.AgencyId) && (caller.IsSuperAdmin() || request.AgencyId == caller.AgencyId) ? request.AgencyId : caller.AgencyId;
        var granted = await db.EffectivePermissionsAsync(caller.UserId, caller.Roles, cancellationToken);
        bool Has(string permission) => caller.IsSuperAdmin() || granted.Contains(permission);
        var now = clock.UtcNow;
        var alerts = new StaffAlertsDto(await StaffAlertQueries.StuckInRetryAsync(db, agency, Sample, cancellationToken),
            await StaffAlertQueries.StaleRoutesAsync(db, agency, now, Sample, cancellationToken), agency);
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
