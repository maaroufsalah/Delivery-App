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

/// <summary>`PATCH /products/:id` : la fiche entière (même schéma que la création).</summary>
public sealed record UpdateProductCommand : ProductFields, IRequest<ProductDetailDto>
{
    [JsonIgnore]
    public string Id { get; init; } = string.Empty;
}

public sealed class UpdateProductValidator : ProductFieldsValidator<UpdateProductCommand>;

internal sealed class UpdateProductHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<UpdateProductCommand, ProductDetailDto>
{
    public async Task<ProductDetailDto> Handle(UpdateProductCommand input, CancellationToken cancellationToken)
    {
        var product = await WarehouseScope.Products(db.Products, caller).FirstOrDefaultAsync(p => p.Id == input.Id && p.DeletedAt == null, cancellationToken)
                      ?? throw BusinessException.NotFound("Produit introuvable.");
        var clientId = await ProductWrites.ResolveClientAsync(db, caller, input.ClientId!, cancellationToken);
        await ProductWrites.AssertReferenceFreeAsync(db, clientId, input.Reference, product.Id, cancellationToken);
        product.ClientId = clientId;
        input.Apply(product);
        db.ProductAttributeAssignments.RemoveRange(await db.ProductAttributeAssignments.Where(a => a.ProductId == product.Id).ToListAsync(cancellationToken));
        await ProductWrites.WriteAttributesAsync(db, product.Id, input.AttributeValueIds, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await ProductRows.DetailAsync(db, caller, mapper, product.Id, cancellationToken);
    }
}
