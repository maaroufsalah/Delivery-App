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

/// <summary>`ProviderTokenListItem`.</summary>
public sealed record ProviderTokenListItemDto(string Id, string Name, string CourierId, string CourierName, string TokenPrefix, string Mode, int RateLimitPerMinute,
    IReadOnlyDictionary<string, int?> StatusMap, DateTime? PreviousExpiresAt, DateTime? RevokedAt, DateTime? RotatedAt, DateTime? LastUsedAt, DateTime CreatedAt, int OpenAlerts);

/// <summary>`ProviderTokenSecret` : le jeton n'est montré qu'une fois.</summary>
public sealed record ProviderTokenSecretDto(ProviderTokenListItemDto Item, string Token);

/// <summary>`ProviderCallListItem`.</summary>
public sealed record ProviderCallListItemDto(string Id, DateTime At, string? EventKey, string ParcelCode, int ProviderStatus, int? MappedStatus, DateTime? OccurredAt, string Mode,
    string Outcome, int HttpStatus, string? Reason, bool Alert, DateTime? AlertResolvedAt);

/// <summary>`provider-token.ts` : jeton de 64 hexadécimaux, empreinte SHA-256, 24 h de grâce à la rotation.</summary>
public static class ProviderTokens
{
    public static readonly TimeSpan RotationGrace = TimeSpan.FromHours(24);
    public const int CallsLimit = 50;

    public static string Generate() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static string PrefixOf(string token) => token[..8];

    public static async Task<ProviderTokenListItemDto> ItemAsync(IAppDbContext db, string id, CancellationToken cancellationToken)
    {
        var row = await db.ProviderTokens.AsNoTracking().Where(t => t.Id == id)
            .Select(t => new { Token = t, t.Courier.FirstName, t.Courier.LastName, Alerts = t.Calls.Count(c => c.Alert && c.AlertResolvedAt == null) })
            .FirstAsync(cancellationToken);
        return ToItem(row.Token, row.FirstName, row.LastName, row.Alerts);
    }

    public static ProviderTokenListItemDto ToItem(ProviderToken t, string firstName, string lastName, int alerts) => new(
        t.Id, t.Name, t.CourierId, $"{firstName} {lastName}".Trim(), t.TokenPrefix, t.Mode.ToString(), t.RateLimitPerMinute,
        ProviderStatusMap.Effective(t.StatusMap).ToDictionary(e => e.Key.ToString(CultureInfo.InvariantCulture), e => e.Value),
        t.PreviousExpiresAt, t.RevokedAt, t.RotatedAt, t.LastUsedAt, t.CreatedAt, alerts);

    public static async Task<ProviderToken> FindAsync(IAppDbContext db, string id, CancellationToken cancellationToken) =>
        await db.ProviderTokens.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
        ?? throw BusinessException.NotFound("Prestataire introuvable.", new { reason = "provider-not-found" });

    public static async Task<ProviderToken> ActiveAsync(IAppDbContext db, string id, CancellationToken cancellationToken)
    {
        var row = await FindAsync(db, id, cancellationToken);
        if (row.RevokedAt is not null) throw BusinessException.Conflict("Ce jeton est révoqué : créez-en un nouveau.", new { reason = "provider-token-revoked" });
        return row;
    }
}
