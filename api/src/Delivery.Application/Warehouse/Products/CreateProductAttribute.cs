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

/// <summary>`POST /products/attributes` : un attribut (Taille, Couleur…), unique par nom.</summary>
public sealed record CreateProductAttributeCommand : IRequest<IReadOnlyList<ProductAttributeItemDto>>
{
    public string? Name { get; init => field = Zod.Trim(value); }
}

public sealed class CreateProductAttributeValidator : ZodValidator<CreateProductAttributeCommand>
{
    public CreateProductAttributeValidator()
    {
        RuleFor(x => x.Name).Required().MinLength(2, "too-short").MaxLength(60);
    }
}

internal sealed class CreateProductAttributeHandler(IAppDbContext db) : IRequestHandler<CreateProductAttributeCommand, IReadOnlyList<ProductAttributeItemDto>>
{
    public async Task<IReadOnlyList<ProductAttributeItemDto>> Handle(CreateProductAttributeCommand input, CancellationToken cancellationToken)
    {
        db.ProductAttributes.Add(new ProductAttribute { Name = input.Name! });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException error) when (DbErrors.IsUniqueViolation(error))
        {
            throw BusinessException.Conflict($"L'attribut « {input.Name} » existe déjà.", new { field = "name" });
        }
        return await ProductRows.AttributesAsync(db, cancellationToken);
    }
}
