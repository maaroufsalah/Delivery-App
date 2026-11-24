using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Application.Parcels;
using Delivery.Application.Warehouse.Stock;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Warehouse.Orders;

/// <summary>`GET /orders/:code`.</summary>
public sealed record GetOrderQuery(string Code) : IRequest<OrderDetailDto>;

internal sealed class GetOrderHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetOrderQuery, OrderDetailDto>
{
    public Task<OrderDetailDto> Handle(GetOrderQuery request, CancellationToken cancellationToken) => OrderRows.DetailAsync(db, caller, request.Code, cancellationToken);
}
