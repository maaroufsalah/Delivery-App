using Delivery.Api.Auth;
using Delivery.Application.BulkJobs;
using MediatR;

namespace Delivery.Api.Background;

/// <summary>
/// `BulkJobsService` côté hôte : reprend au démarrage les tâches restées actives, puis traite la file
/// une tâche à la fois, sous l'identité du demandeur (`bulk-job:{id}` comme identifiant de requête).
/// </summary>
public sealed class BulkJobWorker(BulkJobQueue queue, IServiceScopeFactory scopes, ILogger<BulkJobWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        try
        {
            using var scope = scopes.CreateScope();
            foreach (var id in await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetActiveBulkJobsQuery(), stoppingToken)) queue.Enqueue(id);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(error, "Reprise des tâches de masse impossible");
        }
        await foreach (var id in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                var caller = await sender.Send(new GetBulkJobCallerQuery(id), stoppingToken);
                if (caller is null) continue;
                CurrentUser.Background.Value = (new AuthenticatedUser(caller.UserId, caller.CompanyId, caller.Email, caller.Roles, caller.AgencyId, caller.ClientId, caller.CourierId,
                    false, false, null), $"bulk-job:{id}");
                await sender.Send(new ProcessBulkJobCommand(id), stoppingToken);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                logger.LogError(error, "Tâche de masse {Id} non traitée", id);
            }
            finally
            {
                CurrentUser.Background.Value = null;
            }
        }
    }
}
