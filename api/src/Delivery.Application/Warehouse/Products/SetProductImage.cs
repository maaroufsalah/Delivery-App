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

/// <summary>`POST /products/:id/image` : image nettoyée (1600 px), l'ancienne supprimée.</summary>
public sealed record SetProductImageCommand : IRequest<ProductDetailDto>
{
    public static readonly string[] MimeTypes = ["image/jpeg", "image/png", "image/webp"];

    [JsonIgnore]
    public string Id { get; init; } = string.Empty;

    public string? Name { get; init => field = Zod.Trim(value); }
    public string? MimeType { get; init; }
    public string? Content { get; init; }
}

public sealed class SetProductImageValidator : ZodValidator<SetProductImageCommand>
{
    public SetProductImageValidator()
    {
        RuleFor(x => x.Name).Required().MinLength(1, "required").MaxLength(120);
        RuleFor(x => x.MimeType).Must(v => v is not null && SetProductImageCommand.MimeTypes.Contains(v)).WithMessage("unsupported-file-type");
        RuleFor(x => x.Content).Required().MinLength(1, "required");
    }
}

internal sealed class SetProductImageHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper, IFileStorage storage, ILogger<SetProductImageHandler> logger)
    : IRequestHandler<SetProductImageCommand, ProductDetailDto>
{
    public async Task<ProductDetailDto> Handle(SetProductImageCommand input, CancellationToken cancellationToken)
    {
        var product = await WarehouseScope.Products(db.Products, caller).FirstOrDefaultAsync(p => p.Id == input.Id && p.DeletedAt == null, cancellationToken)
                      ?? throw BusinessException.NotFound("Produit introuvable.");
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(input.Content!);
        }
        catch (FormatException)
        {
            bytes = [];
        }
        var image = storage.SanitizeImage(bytes, input.MimeType!, 1600, preserveFormat: true);
        var stored = await storage.PutAsync($"{caller.CompanyId}/products", image.Content, image.MimeType, cancellationToken);
        var previous = product.ImageKey;
        product.ImageKey = stored.Key;
        product.ImageUrl = $"/api/products/{product.Id}/image";
        await db.SaveChangesAsync(cancellationToken);
        if (previous is not null)
        {
            try
            {
                await storage.RemoveAsync(previous, cancellationToken);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                logger.LogWarning(error, "ancienne image non supprimée");
            }
        }
        return await ProductRows.DetailAsync(db, caller, mapper, product.Id, cancellationToken);
    }
}
