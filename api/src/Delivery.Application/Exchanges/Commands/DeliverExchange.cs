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

/// <summary>`POST /exchanges/:code/deliver` : le nouveau produit est livré.</summary>
public sealed record DeliverExchangeCommand : ExchangeStepCommand;

public sealed class DeliverExchangeValidator : ExchangeStepValidator<DeliverExchangeCommand>;

internal sealed class DeliverExchangeHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<DeliverExchangeCommand, ExchangeDetailDto>
{
    public Task<ExchangeDetailDto> Handle(DeliverExchangeCommand input, CancellationToken cancellationToken) =>
        ExchangeWrites.AdvanceAsync(db, caller, mapper, input, ExchangeStatus.NOUVEAU_LIVRE, ExchangeStatus.ANCIEN_RECUPERE, (x, now) => x.DeliveredAt = now, cancellationToken);
}
