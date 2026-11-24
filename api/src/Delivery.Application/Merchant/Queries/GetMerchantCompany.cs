using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

using Delivery.Application.Merchant.Dtos;

namespace Delivery.Application.Merchant.Queries;

/// <summary>`GET /merchant/me/company`.</summary>
public sealed record GetMerchantCompanyQuery : IRequest<MerchantCompanyDto>;

internal sealed class GetMerchantCompanyHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetMerchantCompanyQuery, MerchantCompanyDto>
{
    public Task<MerchantCompanyDto> Handle(GetMerchantCompanyQuery request, CancellationToken cancellationToken) => LoadAsync(db, caller, cancellationToken);

    internal static string RequireClient(ICurrentUser caller) =>
        caller.ClientId ?? throw BusinessException.Forbidden("Cet espace est réservé aux e-commerçants.", new { reason = "not-a-merchant" });

    internal static async Task<MerchantCompanyDto> LoadAsync(IAppDbContext db, ICurrentUser caller, CancellationToken cancellationToken)
    {
        var clientId = RequireClient(caller);
        var client = await db.Clients.AsNoTracking().Where(c => c.Id == clientId && c.DeletedAt == null)
            .Select(c => new { c.Name, c.Phone, c.Email, c.CityId, CityName = c.City.Name, c.Address, c.IsPro, c.BillingType, c.AgencyId, c.PickupFee, c.FreePickupThreshold })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw BusinessException.NotFound("Fiche client introuvable.");
        var policy = await db.PricingPolicyAsync(cancellationToken, client.AgencyId);
        var fee = client.PickupFee is null ? policy.PickupFee : Money.ToCents(client.PickupFee);
        var freeFrom = client.FreePickupThreshold ?? policy.FreePickupThreshold;
        return new MerchantCompanyDto(
            client.Name, client.Phone, client.Email, client.CityId, client.CityName, client.Address, client.IsPro,
            client.BillingType == BillingType.PROFESSIONAL ? "professional" : "normal",
            new PickupTermsDto(fee, freeFrom));
    }
}
