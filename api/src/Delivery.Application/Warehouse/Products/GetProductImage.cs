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

/// <summary>`GET /products/:id/image`.</summary>
public sealed record GetProductImageQuery(string Id) : IRequest<FileContent>;

internal sealed class GetProductImageHandler(IAppDbContext db, ICurrentUser caller, IFileStorage storage) : IRequestHandler<GetProductImageQuery, FileContent>
{
    public async Task<FileContent> Handle(GetProductImageQuery request, CancellationToken cancellationToken)
    {
        var key = await WarehouseScope.Products(db.Products.AsNoTracking(), caller).Where(p => p.Id == request.Id).Select(p => p.ImageKey).FirstOrDefaultAsync(cancellationToken)
                  ?? throw BusinessException.NotFound("Ce produit n’a pas d’image.");
        var content = await storage.GetAsync(key, cancellationToken);
        var extension = key[(key.LastIndexOf('.') + 1)..];
        var mime = extension switch { "png" => "image/png", "webp" => "image/webp", _ => "image/jpeg" };
        return new FileContent(content, mime, string.Empty, Inline: true, CacheControl: "private, max-age=300");
    }
}
