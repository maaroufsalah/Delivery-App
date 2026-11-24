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

/// <summary>`POST /products/attributes/values` : une valeur d'attribut, unique par attribut.</summary>
public sealed record CreateProductAttributeValueCommand : IRequest<IReadOnlyList<ProductAttributeItemDto>>
{
    public string? AttributeId { get; init; }
    public string? Value { get; init => field = Zod.Trim(value); }
}

public sealed class CreateProductAttributeValueValidator : ZodValidator<CreateProductAttributeValueCommand>
{
    public CreateProductAttributeValueValidator()
    {
        RuleFor(x => x.AttributeId).Required().MinLength(1, "required");
        RuleFor(x => x.Value).Required().MinLength(1, "required").MaxLength(60);
    }
}

internal sealed class CreateProductAttributeValueHandler(IAppDbContext db) : IRequestHandler<CreateProductAttributeValueCommand, IReadOnlyList<ProductAttributeItemDto>>
{
    public async Task<IReadOnlyList<ProductAttributeItemDto>> Handle(CreateProductAttributeValueCommand input, CancellationToken cancellationToken)
    {
        if (!await db.ProductAttributes.AnyAsync(a => a.Id == input.AttributeId, cancellationToken)) throw BusinessException.NotFound("Attribut introuvable.");
        db.ProductAttributeValues.Add(new ProductAttributeValue { AttributeId = input.AttributeId!, Value = input.Value! });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException error) when (DbErrors.IsUniqueViolation(error))
        {
            throw BusinessException.Conflict($"La valeur « {input.Value} » existe déjà.", new { field = "value" });
        }
        return await ProductRows.AttributesAsync(db, cancellationToken);
    }
}
