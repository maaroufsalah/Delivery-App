using AutoMapper;
using Delivery.Application.Clients.Dtos;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Clients.Queries;

/// <summary>`GET /clients/:id`.</summary>
public sealed record GetClientQuery(string Id) : IRequest<ClientDetailDto>;

internal sealed class GetClientHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<GetClientQuery, ClientDetailDto>
{
    public Task<ClientDetailDto> Handle(GetClientQuery request, CancellationToken cancellationToken) => ClientRows.DetailAsync(db, caller, mapper, request.Id, cancellationToken);
}
