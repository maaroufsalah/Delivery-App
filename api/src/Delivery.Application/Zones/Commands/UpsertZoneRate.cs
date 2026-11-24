using AutoMapper;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.Users.Dtos;
using Delivery.Application.Zones.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Zones.Commands;

/// <summary>`POST /zones/rates` : crée ou remplace le tarif d'une zone vers une autre.</summary>
public sealed record UpsertZoneRateCommand : IRequest<ZoneRateItemDto>
{
    public string? FromZoneId { get; init; }

    public string? ToZoneId { get; init; }

    public double? Rate { get; init; }

    public double? DeliveryDays { get; init; } = 2;

    public bool? IsActive { get; init; } = true;
}

public sealed class UpsertZoneRateValidator : ZodValidator<UpsertZoneRateCommand>
{
    public UpsertZoneRateValidator()
    {
        RuleFor(x => x.FromZoneId).Required().MinLength(1, "required");
        RuleFor(x => x.ToZoneId).Required().MinLength(1, "required");
        RuleFor(x => x.Rate).MadAmount();
        RuleFor(x => x.DeliveryDays).Int().Min(0, "negative").Max(30, "too-large");
    }
}

internal sealed class UpsertZoneRateHandler(IAppDbContext db) : IRequestHandler<UpsertZoneRateCommand, ZoneRateItemDto>
{
    public async Task<ZoneRateItemDto> Handle(UpsertZoneRateCommand input, CancellationToken cancellationToken)
    {
        if (input.FromZoneId == input.ToZoneId)
        {
            throw BusinessException.Validation("Le tarif d’une zone vers elle-même se règle sur la zone (tarif intra-ville).", new { field = "toZoneId" });
        }
        var ids = new[] { input.FromZoneId!, input.ToZoneId! };
        if (await db.Zones.CountAsync(z => ids.Contains(z.Id) && z.DeletedAt == null, cancellationToken) != 2)
        {
            throw BusinessException.Validation("Zone inconnue.", new { field = "fromZoneId" });
        }
        var rate = await db.ZoneRates.FirstOrDefaultAsync(r => r.FromZoneId == input.FromZoneId && r.ToZoneId == input.ToZoneId, cancellationToken);
        if (rate is null)
        {
            rate = new ZoneRate { FromZoneId = input.FromZoneId!, ToZoneId = input.ToZoneId! };
            db.ZoneRates.Add(rate);
        }
        rate.Rate = Money.FromMad(input.Rate!.Value);
        rate.DeliveryDays = (int)(input.DeliveryDays ?? 2);
        rate.IsActive = input.IsActive ?? true;
        await db.SaveChangesAsync(cancellationToken);
        return new ZoneRateItemDto(rate.Id, rate.FromZoneId, rate.ToZoneId, Money.ToCents(rate.Rate), rate.DeliveryDays, rate.IsActive);
    }
}
