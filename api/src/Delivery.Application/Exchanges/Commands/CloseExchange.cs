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

/// <summary>`POST /exchanges/:code/close`.</summary>
public sealed record CloseExchangeCommand : ExchangeStepCommand;

public sealed class CloseExchangeValidator : ExchangeStepValidator<CloseExchangeCommand>;

internal sealed class CloseExchangeHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<CloseExchangeCommand, ExchangeDetailDto>
{
    public Task<ExchangeDetailDto> Handle(CloseExchangeCommand input, CancellationToken cancellationToken) =>
        ExchangeWrites.AdvanceAsync(db, caller, mapper, input, ExchangeStatus.CLOTURE, ExchangeStatus.NOUVEAU_LIVRE, (x, now) => x.ClosedAt = now, cancellationToken);
}
