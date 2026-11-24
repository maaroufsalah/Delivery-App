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

/// <summary>`POST /exchanges/:code/cancel` : tant que le nouveau colis n'est pas livré.</summary>
public sealed record CancelExchangeCommand : ExchangeStepCommand;

public sealed class CancelExchangeValidator : ExchangeStepValidator<CancelExchangeCommand>;

internal sealed class CancelExchangeHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<CancelExchangeCommand, ExchangeDetailDto>
{
    public async Task<ExchangeDetailDto> Handle(CancelExchangeCommand input, CancellationToken cancellationToken)
    {
        ExchangeRows.AssertAgency(caller, "Seule l’agence annule un échange.");
        var exchange = await ExchangeRows.Scoped(db.Exchanges, caller).FirstOrDefaultAsync(x => x.Code == input.Code, cancellationToken)
                       ?? throw BusinessException.NotFound($"Échange introuvable : {input.Code}");
        if (!ParcelAdmin.CanCancelExchange(exchange.Status))
        {
            throw BusinessException.Conflict("Le nouveau colis est livré : cet échange ne s’annule plus.", new { reason = "exchange-delivered" });
        }
        await ExchangeWrites.WriteAsync(db, caller, exchange, ExchangeStatus.ANNULE, input.Comment, x => x.CancelledAt = DateTime.UtcNow, cancellationToken);
        return await ExchangeRows.DetailAsync(db, caller, mapper, input.Code, cancellationToken);
    }
}
