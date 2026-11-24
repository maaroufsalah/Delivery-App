using Delivery.Application.Parcels.Labels;
using MediatR;

namespace Delivery.Api.Background;

/// <summary>
/// `LabelJobsService` côté hôte : rend les travaux de la file un par un, et fait le
/// ménage (interrompus au démarrage, expirés toutes les heures).
/// </summary>
public sealed class LabelJobWorker(LabelJobQueue queue, IServiceScopeFactory scopes, ILogger<LabelJobWorker> logger) : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await SweepAsync(recover: true, stoppingToken);
        var sweeper = SweepLoopAsync(stoppingToken);
        await foreach (var work in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ProcessLabelJobCommand(work), stoppingToken);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                logger.LogError(error, "Travail d'étiquettes {Id} non traité", work.JobId);
            }
        }
        await sweeper;
    }

    private async Task SweepLoopAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SweepInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken)) await SweepAsync(recover: false, stoppingToken);
    }

    private async Task SweepAsync(bool recover, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new SweepLabelJobsCommand(recover), stoppingToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(error, "Ménage des étiquettes échoué");
        }
    }
}
