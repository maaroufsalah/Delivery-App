using Delivery.Domain.Identity;

namespace Delivery.Domain.Entities;

public partial class RefreshSession
{
    /// <summary>Nouvelle session de rafraîchissement, horodatée à la seconde comme le `iat` JWT.</summary>
    public static RefreshSession Open(string userId, string familyId, string kind, long iatSeconds, SessionDurations durations, DateTime now) => new()
    {
        Id = Guid.NewGuid().ToString(),
        UserId = userId,
        FamilyId = familyId,
        Kind = kind,
        IssuedAt = DateTime.UnixEpoch.AddSeconds(iatSeconds),
        AccessExpiresAt = DateTime.UnixEpoch.AddSeconds(iatSeconds + durations.AccessSeconds),
        ExpiresAt = DateTime.UnixEpoch.AddSeconds(iatSeconds + SessionPolicy.RefreshSecondsFor(kind, durations)),
        CreatedAt = now,
    };

    public bool IsAlive => RotatedAt is null && RevokedAt is null;

    public static long ToSeconds(DateTime value) => (long)Math.Floor((value - DateTime.UnixEpoch).TotalSeconds);
}
