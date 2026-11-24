using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Parcels;

/// <summary>
/// `ParcelCodeService` : un numéro par (jour UTC, départ, arrivée), réservé
/// atomiquement par `INSERT … ON CONFLICT … RETURNING`.
/// Format `buildParcelCode` : ORIG + jj + rang(3) + aa + mm + DEST.
/// </summary>
public static class ParcelCodes
{
    public static async Task<string> ReserveAsync(IAppDbContext db, string originCityId, string destinationCityId, DateTime at, CancellationToken cancellationToken)
    {
        var codes = await ReserveManyAsync(db, originCityId, destinationCityId, at, 1, cancellationToken);
        return codes[0];
    }

    public static async Task<IReadOnlyList<string>> ReserveManyAsync(IAppDbContext db, string originCityId, string destinationCityId, DateTime at, int count, CancellationToken cancellationToken)
    {
        if (count <= 0) return [];
        var day = DateOnly.FromDateTime(at);
        var rows = await db.Database.SqlQuery<int>($"""
            INSERT INTO "parcel_code_sequences" ("day", "originCityId", "destinationCityId", "lastRank")
            VALUES ({day}, {originCityId}, {destinationCityId}, {count})
            ON CONFLICT ("day", "originCityId", "destinationCityId")
            DO UPDATE SET "lastRank" = "parcel_code_sequences"."lastRank" + {count}
            RETURNING "lastRank" AS "Value"
            """).ToListAsync(cancellationToken);
        if (rows.Count == 0) throw BusinessException.Conflict(count == 1 ? "Impossible de réserver un numéro de colis." : "Impossible de réserver les numéros de colis.");
        var (origin, destination) = await CityCodesAsync(db, originCityId, destinationCityId, cancellationToken);
        var last = rows[0];
        return Enumerable.Range(last - count + 1, count).Select(rank => Build(origin, destination, rank, at)).ToList();
    }

    /// <summary>`preview` : le prochain numéro, sans le réserver.</summary>
    public static async Task<string> PreviewAsync(IAppDbContext db, string originCityId, string destinationCityId, DateTime at, CancellationToken cancellationToken)
    {
        var day = DateOnly.FromDateTime(at);
        var last = await db.ParcelCodeSequences.AsNoTracking()
            .Where(s => s.Day == day && s.OriginCityId == originCityId && s.DestinationCityId == destinationCityId)
            .Select(s => (int?)s.LastRank).FirstOrDefaultAsync(cancellationToken);
        var (origin, destination) = await CityCodesAsync(db, originCityId, destinationCityId, cancellationToken);
        return Build(origin, destination, (last ?? 0) + 1, at);
    }

    public static string Build(string originCode, string destinationCode, int rank, DateTime at) =>
        $"{originCode.ToUpperInvariant()}{at.Day:00}{Pad(rank, 3)}{at.Year % 100:00}{at.Month:00}{destinationCode.ToUpperInvariant()}";

    /// <summary>`String(n).padStart(width, '0')` : un rang au-delà de 999 n'est pas tronqué.</summary>
    private static string Pad(int value, int width) => value.ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(width, '0');

    private static async Task<(string Origin, string Destination)> CityCodesAsync(IAppDbContext db, string originCityId, string destinationCityId, CancellationToken cancellationToken)
    {
        var cities = await db.Cities.AsNoTracking().Where(c => c.Id == originCityId || c.Id == destinationCityId)
            .Select(c => new { c.Id, c.Code }).ToListAsync(cancellationToken);
        var origin = cities.FirstOrDefault(c => c.Id == originCityId)?.Code;
        var destination = cities.FirstOrDefault(c => c.Id == destinationCityId)?.Code;
        if (string.IsNullOrEmpty(origin) || string.IsNullOrEmpty(destination))
        {
            throw BusinessException.Validation("Ville de départ ou de destination inconnue.", new { originCityId, destinationCityId });
        }
        return (origin, destination);
    }
}
