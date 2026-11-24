using System.Text.Json;
using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Common.Idempotency;

/// <summary>
/// `IdempotencyService.replay` : la file hors-ligne du téléphone renvoie un geste tant qu'elle n'a pas de réponse.
/// La première réponse rendue pour une clé est mémorisée par utilisateur ; un rejeu la rend telle quelle. Un refus
/// n'est pas mémorisé. Deux envois simultanés de la même clé : le second attend le premier sous un verrou
/// consultatif Postgres (de session, sur la connexion de la requête) — jamais un verrou en mémoire, l'API tourne
/// sur plusieurs instances.
/// </summary>
public static class IdempotentReplay
{
    public static async Task<object?> RunAsync<T>(IAppDbContext db, ICurrentUser caller, string? key, string path, Func<Task<T>> work, CancellationToken cancellationToken)
    {
        var trimmed = key?.Trim();
        path = path.Length > 200 ? path[..200] : path;
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > 80) return await work();

        var lockKey = $"idempotency:{caller.UserId}:{trimmed}";
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_lock(hashtext({lockKey}))", cancellationToken);
            try
            {
                var seen = await db.IdempotentRequests.AsNoTracking().FirstOrDefaultAsync(r => r.UserId == caller.UserId && r.Key == trimmed, cancellationToken);
                if (seen is not null)
                {
                    if (!seen.Path.Contains(':') && seen.Path != path) throw ReusedKey();
                    return JsonDocument.Parse(seen.Response).RootElement.Clone();
                }
                var result = await work();
                try
                {
                    // `createdAt` est un `timestamp` sans fuseau (schéma Speedy) : l'heure UTC s'y écrit sans « Kind ».
                    db.IdempotentRequests.Add(new IdempotentRequest
                    {
                        UserId = caller.UserId, Key = trimmed, Path = path, Response = JsonSerializer.Serialize<object?>(result, JsonSerializerOptions.Web),
                        CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                    });
                    await db.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException)
                {
                    // Une autre instance a mémorisé la même clé entre-temps : la réponse rendue reste la nôtre.
                    db.ChangeTracker.Clear();
                }
                return result;
            }
            finally
            {
                await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_unlock(hashtext({lockKey}))", CancellationToken.None);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static BusinessException ReusedKey() =>
        BusinessException.Validation("Cette clé d’envoi a déjà servi pour une autre opération.", new { reason = "idempotency-key-reused" });
}
