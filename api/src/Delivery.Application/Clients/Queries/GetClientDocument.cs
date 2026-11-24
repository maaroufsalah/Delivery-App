using AutoMapper;
using Delivery.Application.Clients.Dtos;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Clients.Queries;

/// <summary>`GET /clients/:id/documents/:documentId` : le fichier, en pièce jointe.</summary>
public sealed record GetClientDocumentQuery(string ClientId, string DocumentId) : IRequest<FileContent>;

internal sealed class GetClientDocumentHandler(IAppDbContext db, IFileStorage storage) : IRequestHandler<GetClientDocumentQuery, FileContent>
{
    public async Task<FileContent> Handle(GetClientDocumentQuery request, CancellationToken cancellationToken)
    {
        await ClientRows.FindAsync(db, request.ClientId, cancellationToken);
        var document = await db.ClientDocuments.AsNoTracking()
            .Where(d => d.Id == request.DocumentId && d.ClientId == request.ClientId && d.DeletedAt == null)
            .Select(d => new { d.StorageKey, d.MimeType, d.Name })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw Domain.Common.BusinessException.NotFound("Document introuvable.");
        var content = await storage.GetAsync(document.StorageKey, cancellationToken);
        return new FileContent(content, document.MimeType, Uri.EscapeDataString(document.Name), CacheControl: "private, no-store");
    }
}
