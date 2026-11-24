using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Claims;

/// <summary>`attachment` : une pièce jointe de la réclamation ; celle d'une note interne reste au personnel.</summary>
public sealed record GetClaimAttachmentQuery(string Code, string Id) : IRequest<FileContent>;

internal sealed class GetClaimAttachmentHandler(IAppDbContext db, ICurrentUser caller, IFileStorage storage) : IRequestHandler<GetClaimAttachmentQuery, FileContent>
{
    public async Task<FileContent> Handle(GetClaimAttachmentQuery input, CancellationToken cancellationToken)
    {
        var claims = (await ClaimRows.ScopedAsync(db, db.Claims.AsNoTracking(), caller, cancellationToken)).Where(c => c.Code == input.Code).Select(c => c.Id);
        var row = await db.ClaimAttachments.AsNoTracking().Where(a => a.Id == input.Id && claims.Contains(a.ClaimId))
            .Select(a => new { a.Filename, a.MimeType, a.StorageKey, Internal = a.Event != null && a.Event.IsInternal }).FirstOrDefaultAsync(cancellationToken);
        if (row is null || (row.Internal && !ClaimRows.CanHandle(caller))) throw BusinessException.NotFound("Pièce jointe introuvable.");
        return new FileContent(await storage.GetAsync(row.StorageKey, cancellationToken), row.MimeType, row.Filename, CacheControl: "private, no-store");
    }
}
