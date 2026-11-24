using AutoMapper;
using Delivery.Application.Clients.Dtos;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Clients.Commands;

/// <summary>`DELETE /clients/:id/documents/:documentId` : fichier effacé du volume.</summary>
public sealed record RemoveClientDocumentCommand(string ClientId, string DocumentId) : IRequest<ClientDetailDto>;

internal sealed class RemoveClientDocumentHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IFileStorage storage, IMapper mapper) : IRequestHandler<RemoveClientDocumentCommand, ClientDetailDto>
{
    public async Task<ClientDetailDto> Handle(RemoveClientDocumentCommand request, CancellationToken cancellationToken)
    {
        await ClientRows.FindAsync(db, request.ClientId, cancellationToken);
        var document = await db.ClientDocuments.FirstOrDefaultAsync(d => d.Id == request.DocumentId && d.ClientId == request.ClientId && d.DeletedAt == null, cancellationToken)
            ?? throw BusinessException.NotFound("Document introuvable.");
        document.DeletedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await storage.RemoveAsync(document.StorageKey, cancellationToken);
        return await ClientRows.DetailAsync(db, caller, mapper, request.ClientId, cancellationToken);
    }
}
