using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Providers;

/// <summary>`POST /settings/providers/:id/revoke` : définitif, l'ancien jeton aussi.</summary>
public sealed record RevokeProviderTokenCommand(string Id) : IRequest<ProviderTokenListItemDto>;

internal sealed class RevokeProviderTokenHandler(IAppDbContext db, IClock clock) : IRequestHandler<RevokeProviderTokenCommand, ProviderTokenListItemDto>
{
    public async Task<ProviderTokenListItemDto> Handle(RevokeProviderTokenCommand request, CancellationToken cancellationToken)
    {
        var row = await ProviderTokens.FindAsync(db, request.Id, cancellationToken);
        if (row.RevokedAt is null)
        {
            row.RevokedAt = clock.UtcNow;
            row.PreviousExpiresAt = null;
            await db.SaveChangesAsync(cancellationToken);
        }
        return await ProviderTokens.ItemAsync(db, row.Id, cancellationToken);
    }
}
