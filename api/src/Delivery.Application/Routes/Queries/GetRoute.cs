using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Routes;

/// <summary>`GET /routes/:code`.</summary>
public sealed record GetRouteQuery(string Code) : IRequest<RouteDetailDto>;

internal sealed class GetRouteHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetRouteQuery, RouteDetailDto>
{
    public Task<RouteDetailDto> Handle(GetRouteQuery request, CancellationToken cancellationToken) => RouteRows.DetailAsync(db, caller, request.Code, cancellationToken);
}
