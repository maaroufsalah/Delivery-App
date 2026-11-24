using AutoMapper;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Agencies.Queries;

/// <summary>`GET /agencies/:id`.</summary>
public sealed record GetAgencyQuery(string Id) : IRequest<AgencyListItemDto>;

internal sealed class GetAgencyHandler(IAppDbContext db, IMapper mapper) : IRequestHandler<GetAgencyQuery, AgencyListItemDto>
{
    public Task<AgencyListItemDto> Handle(GetAgencyQuery request, CancellationToken cancellationToken) => AgencyRows.LoadAsync(db, mapper, request.Id, cancellationToken);
}
