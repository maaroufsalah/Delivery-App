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

/// <summary>`DELETE /clients/:id/discounts/:discountId`.</summary>
public sealed record RemoveClientDiscountCommand(string ClientId, string DiscountId) : IRequest<ClientDetailDto>;

internal sealed class RemoveClientDiscountHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<RemoveClientDiscountCommand, ClientDetailDto>
{
    public async Task<ClientDetailDto> Handle(RemoveClientDiscountCommand request, CancellationToken cancellationToken)
    {
        await ClientRows.FindAsync(db, request.ClientId, cancellationToken);
        var deleted = await db.ClientDiscounts.Where(d => d.Id == request.DiscountId && d.ClientId == request.ClientId).ExecuteDeleteAsync(cancellationToken);
        if (deleted == 0) throw BusinessException.NotFound("Remise introuvable.");
        return await ClientRows.DetailAsync(db, caller, mapper, request.ClientId, cancellationToken);
    }
}
