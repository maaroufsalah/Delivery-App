using AutoMapper;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Couriers.Queries;

/// <summary>`GET /couriers/:id`.</summary>
public sealed record GetCourierQuery(string Id) : IRequest<CourierDetailDto>;

internal sealed class GetCourierHandler(IAppDbContext db, IMapper mapper) : IRequestHandler<GetCourierQuery, CourierDetailDto>
{
    public Task<CourierDetailDto> Handle(GetCourierQuery request, CancellationToken cancellationToken) => CourierRows.DetailAsync(db, mapper, request.Id, cancellationToken);
}
