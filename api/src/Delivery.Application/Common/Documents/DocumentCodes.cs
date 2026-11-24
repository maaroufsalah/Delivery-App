using System.Globalization;
using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Common.Documents;

/// <summary>`DocumentCodeService` : `KIND-AAMM-0001` (ou `KIND-AA-0001`), un compteur par sorte et période.</summary>
public static class DocumentCodes
{
    public const string ReturnHandover = "RL";
    public const string ReturnSlip = "BRT";
    public const string BlNote = "BRB";
    public const string Exchange = "ECH";

    public static async Task<string> ReserveAsync(IAppDbContext db, string kind, DateTime at, CancellationToken cancellationToken, bool yearly = false)
    {
        var period = PeriodOf(at, yearly);
        var rows = await db.Database.SqlQuery<int>($"""
            INSERT INTO "document_sequences" ("kind", "period", "lastRank")
            VALUES ({kind}, {period}, 1)
            ON CONFLICT ("kind", "period")
            DO UPDATE SET "lastRank" = "document_sequences"."lastRank" + 1
            RETURNING "lastRank" AS "Value"
            """).ToListAsync(cancellationToken);
        if (rows.Count == 0) throw BusinessException.Conflict("Impossible de réserver un numéro de document.");
        return $"{kind}-{period}-{rows[0].ToString(CultureInfo.InvariantCulture).PadLeft(4, '0')}";
    }

    private static string PeriodOf(DateTime at, bool yearly)
    {
        var year = (at.Year % 100).ToString("00", CultureInfo.InvariantCulture);
        return yearly ? year : year + at.Month.ToString("00", CultureInfo.InvariantCulture);
    }
}
