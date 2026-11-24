using Delivery.Infrastructure.Persistence;
using Delivery.Infrastructure.Services;

namespace Delivery.Api.Background;

/// <summary>
/// Envoie les pushes des notifications archivées, par paquets d'une demi-seconde. Une notification écrite dans une
/// transaction annulée n'existe plus en base : elle ne part pas. Inactif en environnement de test (aucun appel à Expo).
/// </summary>
public sealed class PushWorker(PushQueue queue, IServiceScopeFactory scopes, IHostEnvironment environment, ILogger<PushWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (environment.IsEnvironment("Testing")) return;
        while (await queue.Reader.WaitToReadAsync(stoppingToken))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);
            var ids = new List<string>();
            while (ids.Count < 500 && queue.Reader.TryRead(out var id)) ids.Add(id);
            try
            {
                using var scope = scopes.CreateScope();
                await PushDispatch.SendAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), scope.ServiceProvider.GetRequiredService<ExpoPushClient>(), ids, stoppingToken);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                logger.LogWarning("Notifications non envoyées : {Message}", error.Message);
            }
        }
    }
}
