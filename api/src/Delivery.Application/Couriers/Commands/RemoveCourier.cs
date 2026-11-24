using System.Text.Json.Nodes;
using AutoMapper;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.Users;
using Delivery.Application.Users.Commands;
using Delivery.Application.Users.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Couriers.Commands;

/// <summary>`DELETE /couriers/:id` : refusée tant que colis, circuits ou retours de fonds le référencent.</summary>
public sealed record RemoveCourierCommand(string Id) : IRequest<IdResult>;

internal sealed class RemoveCourierHandler(IAppDbContext db, IClock clock) : IRequestHandler<RemoveCourierCommand, IdResult>
{
    public async Task<IdResult> Handle(RemoveCourierCommand request, CancellationToken cancellationToken)
    {
        var id = request.Id;
        var courier = await CourierRows.FindAsync(db, id, cancellationToken);
        References.AssertDeletable($"le livreur « {courier.FirstName} {courier.LastName} »",
            References.Of("parcels", await db.Parcels.CountAsync(p => p.CourierId == id, cancellationToken)),
            References.Of("routes", await db.Routes.CountAsync(r => r.CourierId == id, cancellationToken)),
            References.Of("cashRemittances", await db.CashRemittances.CountAsync(r => r.CourierId == id, cancellationToken)));
        courier.DeletedAt = clock.UtcNow;
        courier.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
        return new IdResult(id);
    }
}
