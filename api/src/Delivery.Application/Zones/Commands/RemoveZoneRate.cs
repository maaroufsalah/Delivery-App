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

/// <summary>`DELETE /zones/rates/:id`.</summary>
public sealed record RemoveZoneRateCommand(string Id) : IRequest<IdResult>;

internal sealed class RemoveZoneRateHandler(IAppDbContext db) : IRequestHandler<RemoveZoneRateCommand, IdResult>
{
    public async Task<IdResult> Handle(RemoveZoneRateCommand request, CancellationToken cancellationToken)
    {
        var deleted = await db.ZoneRates.Where(r => r.Id == request.Id).ExecuteDeleteAsync(cancellationToken);
        if (deleted == 0) throw BusinessException.NotFound("Tarif de zone introuvable.");
        return new IdResult(request.Id);
    }
}
