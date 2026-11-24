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

/// <summary>`POST /products`.</summary>
public sealed record CreateProductCommand : ProductFields, IRequest<ProductDetailDto>;

public sealed class CreateProductValidator : ProductFieldsValidator<CreateProductCommand>;

internal sealed class CreateProductHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<CreateProductCommand, ProductDetailDto>
{
    public async Task<ProductDetailDto> Handle(CreateProductCommand input, CancellationToken cancellationToken)
    {
        var clientId = await ProductWrites.ResolveClientAsync(db, caller, input.ClientId!, cancellationToken);
        await ProductWrites.AssertReferenceFreeAsync(db, clientId, input.Reference, null, cancellationToken);
        var product = new Product { ClientId = clientId };
        input.Apply(product);
        db.Products.Add(product);
        await ProductWrites.WriteAttributesAsync(db, product.Id, input.AttributeValueIds, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await ProductRows.DetailAsync(db, caller, mapper, product.Id, cancellationToken);
    }
}
