using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

using Delivery.Application.Merchant.Dtos;

using Delivery.Application.Merchant.Queries;

namespace Delivery.Application.Merchant.Commands;

/// <summary>`PATCH /merchant/me/company` : nom, téléphone, ville et adresse de la boutique.</summary>
public sealed record UpdateMerchantCompanyCommand : IRequest<MerchantCompanyDto>
{
    public string? Name { get; init => field = Zod.Trim(value); }

    public string? Phone { get; init => field = value is null ? null : Phones.Normalize(value); }

    public string? CityId { get; init; }

    public string? Address { get; init => field = Zod.Trim(value); }
}

public sealed class UpdateMerchantCompanyValidator : ZodValidator<UpdateMerchantCompanyCommand>
{
    public UpdateMerchantCompanyValidator()
    {
        RuleFor(x => x.Name).Required().MinLength(2, "too-short").MaxLength(160);
        RuleFor(x => x.Phone).Required().Pattern(Phones.Pattern, "invalid-phone");
        RuleFor(x => x.CityId).Required().MinLength(1, "required");
        RuleFor(x => x.Address).MaxLength(240);
    }
}

internal sealed class UpdateMerchantCompanyHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<UpdateMerchantCompanyCommand, MerchantCompanyDto>
{
    public async Task<MerchantCompanyDto> Handle(UpdateMerchantCompanyCommand input, CancellationToken cancellationToken)
    {
        var clientId = GetMerchantCompanyHandler.RequireClient(caller);
        if (!await db.Cities.AnyAsync(c => c.Id == input.CityId && c.DeletedAt == null, cancellationToken))
        {
            throw BusinessException.Validation("Ville inconnue.", new { field = "cityId" });
        }
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == clientId && c.DeletedAt == null, cancellationToken);
        if (client is not null)
        {
            client.Name = input.Name!;
            client.Phone = input.Phone!;
            client.CityId = input.CityId!;
            client.Address = input.Address;
            await db.SaveChangesAsync(cancellationToken);
        }
        return await GetMerchantCompanyHandler.LoadAsync(db, caller, cancellationToken);
    }
}
