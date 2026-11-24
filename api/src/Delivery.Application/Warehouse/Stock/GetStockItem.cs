using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Warehouse.Stock;

/// <summary>`GET /stock/:id`.</summary>
public sealed record GetStockItemQuery(string Id) : IRequest<StockDetailDto>;

internal sealed class GetStockItemHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetStockItemQuery, StockDetailDto>
{
    public Task<StockDetailDto> Handle(GetStockItemQuery request, CancellationToken cancellationToken) => StockRows.DetailAsync(db, caller, request.Id, cancellationToken);
}
