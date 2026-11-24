using System.Text.Json.Serialization;
using AutoMapper;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.Exchanges.Dtos;
using Delivery.Application.Parcels;
using Delivery.Application.Parcels.Commands;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Exchanges.Commands;

/// <summary>`POST /exchanges/:code/pickup` : l'ancien produit est récupéré.</summary>
public sealed record PickupExchangeCommand : ExchangeStepCommand;

public sealed class PickupExchangeValidator : ExchangeStepValidator<PickupExchangeCommand>;

internal sealed class PickupExchangeHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<PickupExchangeCommand, ExchangeDetailDto>
{
    public Task<ExchangeDetailDto> Handle(PickupExchangeCommand input, CancellationToken cancellationToken) =>
        ExchangeWrites.AdvanceAsync(db, caller, mapper, input, ExchangeStatus.ANCIEN_RECUPERE, ExchangeStatus.NOUVEAU_COLIS_CREE, (x, now) => x.PickedUpAt = now, cancellationToken);
}
