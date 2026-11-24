using System.Text.Json.Nodes;
using AutoMapper;
using Delivery.Application.Clients.Dtos;
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

namespace Delivery.Application.Clients.Commands;

/// <summary>`DELETE /clients/:id` : refusée tant que des colis, factures ou bons le référencent.</summary>
public sealed record RemoveClientCommand(string Id) : IRequest<IdResult>;

internal sealed class RemoveClientHandler(IAppDbContext db, IClock clock) : IRequestHandler<RemoveClientCommand, IdResult>
{
    public async Task<IdResult> Handle(RemoveClientCommand request, CancellationToken cancellationToken)
    {
        var id = request.Id;
        var client = await ClientRows.FindAsync(db, id, cancellationToken);
        References.AssertDeletable($"le client « {client.Name} »",
            References.Of("parcels", await db.Parcels.CountAsync(p => p.ClientId == id, cancellationToken)),
            References.Of("invoices", await db.Invoices.CountAsync(i => i.ClientId == id, cancellationToken)),
            References.Of("deliveryNotes", await db.DeliveryNotes.CountAsync(n => n.ClientId == id, cancellationToken)));
        client.DeletedAt = clock.UtcNow;
        client.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
        return new IdResult(id);
    }
}
