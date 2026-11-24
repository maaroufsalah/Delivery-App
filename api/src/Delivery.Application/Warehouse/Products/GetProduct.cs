using System.Text.Json.Serialization;
using AutoMapper;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Persistence;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Warehouse.Products;

/// <summary>`GET /products/:id`.</summary>
public sealed record GetProductQuery(string Id) : IRequest<ProductDetailDto>;

internal sealed class GetProductHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<GetProductQuery, ProductDetailDto>
{
    public Task<ProductDetailDto> Handle(GetProductQuery request, CancellationToken cancellationToken) => ProductRows.DetailAsync(db, caller, mapper, request.Id, cancellationToken);
}
