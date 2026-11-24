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

/// <summary>`POST /settings/providers/:id/rotate` : nouveau jeton, l'ancien reste valable 24 h.</summary>
public sealed record RotateProviderTokenCommand(string Id) : IRequest<ProviderTokenSecretDto>;

internal sealed class RotateProviderTokenHandler(IAppDbContext db, IClock clock) : IRequestHandler<RotateProviderTokenCommand, ProviderTokenSecretDto>
{
    public async Task<ProviderTokenSecretDto> Handle(RotateProviderTokenCommand request, CancellationToken cancellationToken)
    {
        var row = await ProviderTokens.ActiveAsync(db, request.Id, cancellationToken);
        var token = ProviderTokens.Generate();
        var now = clock.UtcNow;
        row.PreviousTokenHash = row.TokenHash;
        row.PreviousExpiresAt = now + ProviderTokens.RotationGrace;
        row.TokenHash = ProviderTokens.Hash(token);
        row.TokenPrefix = ProviderTokens.PrefixOf(token);
        row.RotatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return new ProviderTokenSecretDto(await ProviderTokens.ItemAsync(db, row.Id, cancellationToken), token);
    }
}
