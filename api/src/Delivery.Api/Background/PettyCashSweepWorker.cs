using Delivery.Application.PettyCash;
using MediatR;

namespace Delivery.Api.Background;

/// <summary>
/// `PettyCashDaysService.onModuleInit` : premier passage après 10 s, puis chaque minute. Une journée passée encore
/// ouverte se clôt seule. Inactive en environnement de test, où chaque test pilote lui-même le balayage.
/// </summary>
public sealed class PettyCashSweepWorker(IServiceScopeFactory scopes, IHostEnvironment environment, ILogger<PettyCashSweepWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (environment.IsEnvironment("Testing")) return;
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var done = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new SweepPettyCashDaysCommand(), stoppingToken);
                if (done.Created > 0 || done.Closed > 0) logger.LogInformation("Petite caisse : {Created} journée(s) créée(s), {Closed} clôturée(s).", done.Created, done.Closed);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                logger.LogError(error, "Clôture automatique de la petite caisse impossible");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
