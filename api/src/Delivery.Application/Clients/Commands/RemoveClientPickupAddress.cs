using AutoMapper;
using Delivery.Application.Clients.Dtos;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Clients.Commands;

/// <summary>`DELETE /clients/:id/pickup-addresses/:addressId` : suppression douce.</summary>
public sealed record RemoveClientPickupAddressCommand(string ClientId, string AddressId) : IRequest<ClientDetailDto>;

internal sealed class RemoveClientPickupAddressHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IMapper mapper) : IRequestHandler<RemoveClientPickupAddressCommand, ClientDetailDto>
{
    public async Task<ClientDetailDto> Handle(RemoveClientPickupAddressCommand request, CancellationToken cancellationToken)
    {
        await ClientRows.FindAsync(db, request.ClientId, cancellationToken);
        var address = await db.ClientPickupAddresses.FirstOrDefaultAsync(a => a.Id == request.AddressId && a.ClientId == request.ClientId && a.DeletedAt == null, cancellationToken)
            ?? throw BusinessException.NotFound("Adresse introuvable.");
        address.DeletedAt = clock.UtcNow;
        address.IsDefault = false;
        await db.SaveChangesAsync(cancellationToken);
        return await ClientRows.DetailAsync(db, caller, mapper, request.ClientId, cancellationToken);
    }
}
