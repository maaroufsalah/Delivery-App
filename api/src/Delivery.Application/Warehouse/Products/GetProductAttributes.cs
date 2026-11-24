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

/// <summary>`GET /products/attributes`.</summary>
public sealed record GetProductAttributesQuery : IRequest<IReadOnlyList<ProductAttributeItemDto>>;

internal sealed class GetProductAttributesHandler(IAppDbContext db) : IRequestHandler<GetProductAttributesQuery, IReadOnlyList<ProductAttributeItemDto>>
{
    public Task<IReadOnlyList<ProductAttributeItemDto>> Handle(GetProductAttributesQuery request, CancellationToken cancellationToken) => ProductRows.AttributesAsync(db, cancellationToken);
}
