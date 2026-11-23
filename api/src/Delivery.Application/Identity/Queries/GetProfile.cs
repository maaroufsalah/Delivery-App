using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Identity.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Identity.Queries;

/// <summary>`GET /auth/me` : profil, permissions effectives et état de l'espace.</summary>
public sealed record GetProfileQuery : IRequest<UserProfileDto>;

internal sealed class GetProfileHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<GetProfileQuery, UserProfileDto>
{
    public async Task<UserProfileDto> Handle(GetProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == caller.UserId && u.DeletedAt == null)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.Username,
                u.FirstName,
                u.LastName,
                u.Phone,
                Roles = u.Roles.Select(r => r.Role).ToList(),
                u.AgencyId,
                ClientId = u.Client != null ? u.Client.Id : null,
                CourierId = u.Courier != null ? u.Courier.Id : null,
                u.MustChangePassword,
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw BusinessException.NotFound("Utilisateur introuvable.");

        var granted = await db.EffectivePermissionsAsync(user.Id, user.Roles, cancellationToken);
        var company = await db.Companies.AsNoTracking().OrderBy(c => c.CreatedAt).Select(c => new { c.Status, c.TrialEndsAt }).FirstOrDefaultAsync(cancellationToken);
        TenantBanner? tenant = null;
        if (company is not null)
        {
            var now = clock.UtcNow;
            var endsAt = company.TrialEndsAt;
            tenant = new TenantBanner(
                company.Status.ToString(),
                endsAt is null ? null : Iso(endsAt.Value),
                endsAt is null ? null : (int)Math.Ceiling((endsAt.Value - now).TotalMilliseconds / (24d * 60 * 60 * 1000)),
                company.Status == CompanyStatus.TRIAL && endsAt is not null && endsAt.Value <= now);
        }

        return new UserProfileDto(
            user.Id, user.Email, user.Username, user.FirstName, user.LastName, user.Phone,
            user.Roles.Select(r => r.ToString()).ToList(), user.AgencyId, user.ClientId, user.CourierId,
            user.MustChangePassword, granted.ToList(), tenant);
    }

    private static string Iso(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture);
}
