using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Claims;

/// <summary>`ClaimTypesService.remove` : retrait logique, refusé tant que des réclamations l'utilisent.</summary>
public sealed record DeleteClaimTypeCommand(string Id) : IRequest<ClaimIdDto>;

internal sealed class DeleteClaimTypeHandler(IAppDbContext db, IClock clock) : IRequestHandler<DeleteClaimTypeCommand, ClaimIdDto>
{
    public async Task<ClaimIdDto> Handle(DeleteClaimTypeCommand input, CancellationToken cancellationToken)
    {
        var row = await db.ClaimTypes.FirstOrDefaultAsync(t => t.Id == input.Id && t.DeletedAt == null, cancellationToken)
                  ?? throw BusinessException.NotFound("Type de réclamation introuvable.");
        var used = await db.Claims.CountAsync(c => c.TypeId == row.Id, cancellationToken);
        if (used > 0)
        {
            throw BusinessException.Conflict("Ce type est utilisé par des réclamations. Désactivez-le plutôt que de le supprimer.",
                new { references = new[] { new { label = "Réclamations", count = used } } });
        }
        row.DeletedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return new ClaimIdDto(input.Id);
    }
}
