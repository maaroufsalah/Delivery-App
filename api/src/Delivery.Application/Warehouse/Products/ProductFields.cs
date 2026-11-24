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

/// <summary>`productListQuerySchema` une fois lu.</summary>
public sealed record ProductListFilter(ListQuery Paging, string Order, IReadOnlyList<string>? ClientId, IReadOnlyList<string>? AgencyId, string? IsActive);

/// <summary>`productSchema` : la fiche produit (dimensions en mm, poids en g, montants en MAD).</summary>
public abstract record ProductFields
{
    public string? ClientId { get; init; }
    public string? Reference { get; init => field = Zod.Trim(value); }
    public string? Name { get; init => field = Zod.Trim(value); }
    public double? LengthMm { get; init; }
    public double? WidthMm { get; init; }
    public double? HeightMm { get; init; }
    public double? WeightG { get; init; }
    public double? Price { get; init; }
    public double? DeclaredValue { get; init; }
    public double? InternalStock { get; init; }
    public IReadOnlyList<string>? AttributeValueIds { get; init; } = [];
    public bool? IsActive { get; init; } = true;

    public void Apply(Product product)
    {
        product.Reference = string.IsNullOrEmpty(Reference) ? null : Reference;
        product.Name = Name!;
        product.LengthMm = (int?)LengthMm;
        product.WidthMm = (int?)WidthMm;
        product.HeightMm = (int?)HeightMm;
        product.WeightG = (int?)WeightG;
        product.Price = Price is { } price ? Money.FromMad(price) : null;
        product.Value = DeclaredValue is { } value ? Money.FromMad(value) : null;
        product.InternalStock = (int?)InternalStock;
        product.IsActive = IsActive ?? true;
    }
}

public abstract class ProductFieldsValidator<T> : ZodValidator<T>
    where T : ProductFields
{
    protected ProductFieldsValidator()
    {
        RuleFor(x => x.ClientId).Required().MinLength(1, "required");
        RuleFor(x => x.Reference).MaxLength(64);
        RuleFor(x => x.Name).Required().MinLength(2, "too-short").MaxLength(160);
        foreach (var (field, max) in new (System.Linq.Expressions.Expression<Func<T, double?>>, double)[] { (x => x.LengthMm, 10_000), (x => x.WidthMm, 10_000), (x => x.HeightMm, 10_000), (x => x.WeightG, 200_000) })
        {
            RuleFor(field).Int().WithMessage("not-integer").Min(0, "negative").Max(max, "too-large");
        }
        RuleFor(x => x.Price).MadAmount(nullable: true);
        RuleFor(x => x.DeclaredValue).MadAmount(nullable: true);
        RuleFor(x => x.InternalStock).Int().Min(0, "negative");
        RuleFor(x => x.AttributeValueIds).Custom((ids, context) =>
        {
            if (ids is null) return;
            for (var i = 0; i < ids.Count; i++) if (string.IsNullOrEmpty(ids[i])) context.AddFailure(ValidationPaths.Raw + $"attributeValueIds.{i}", Zod.TooSmallString(1));
        });
    }
}

internal static class ProductWrites
{
    public static async Task<string> ResolveClientAsync(IAppDbContext db, ICurrentUser caller, string requested, CancellationToken cancellationToken)
    {
        if (WarehouseScope.IsMerchant(caller)) return caller.ClientId!;
        return await db.Clients.AsNoTracking().Where(c => c.Id == requested && c.DeletedAt == null).Select(c => c.Id).FirstOrDefaultAsync(cancellationToken)
               ?? throw BusinessException.NotFound("E-commerçant introuvable.");
    }

    public static async Task AssertReferenceFreeAsync(IAppDbContext db, string clientId, string? reference, string? exceptId, CancellationToken cancellationToken)
    {
        var value = (reference ?? string.Empty).Trim();
        if (value.Length == 0) return;
        if (await db.Products.AnyAsync(p => p.ClientId == clientId && p.Reference == value && p.DeletedAt == null && (exceptId == null || p.Id != exceptId), cancellationToken))
        {
            throw BusinessException.Conflict($"La référence « {value} » est déjà utilisée.", new { field = "reference" });
        }
    }

    public static async Task WriteAttributesAsync(IAppDbContext db, string productId, IReadOnlyList<string>? valueIds, CancellationToken cancellationToken)
    {
        if (valueIds is not { Count: > 0 }) return;
        var known = await db.ProductAttributeValues.Where(v => valueIds.Contains(v.Id)).Select(v => v.Id).ToListAsync(cancellationToken);
        if (known.Count != valueIds.Count) throw BusinessException.Validation("Valeur d’attribut inconnue.", new { field = "attributeValueIds" });
        foreach (var id in known.Distinct()) db.ProductAttributeAssignments.Add(new ProductAttributeAssignment { ProductId = productId, ValueId = id });
    }
}

public sealed record IdDto(string Id);
