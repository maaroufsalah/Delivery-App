using System.Text.Json;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Routes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.BulkJobs;

/// <summary>
/// `BulkJobsService.process` : les colis restants par morceaux de 250, le progrès enregistré
/// après chaque morceau ; un refus métier (409/422) isole le colis fautif, toute autre erreur arrête la tâche.
/// Exécuté par le service d'arrière-plan sous l'identité du demandeur.
/// </summary>
public sealed record ProcessBulkJobCommand(string Id) : IRequest;

internal sealed class ProcessBulkJobHandler(IAppDbContext db, ICurrentUser caller, IClock clock, ILogger<ProcessBulkJobHandler> logger) : IRequestHandler<ProcessBulkJobCommand>
{
    public async Task Handle(ProcessBulkJobCommand request, CancellationToken cancellationToken)
    {
        var job = await db.BulkJobs.FirstOrDefaultAsync(j => j.Id == request.Id, cancellationToken);
        if (job is null || job.Status is not (BulkJobStatus.PENDING or BulkJobStatus.RUNNING)) return;
        using var input = JsonDocument.Parse(job.Input);
        var all = input.RootElement.TryGetProperty("parcelIds", out var ids) ? ids.EnumerateArray().Select(e => e.GetString()!).ToList() : [];
        if (job.Kind is not (BulkJobKinds.NoteValidate or BulkJobKinds.RouteDispatch or BulkJobKinds.RouteOutcomes or BulkJobKinds.ParcelTransfer))
        {
            await StopAsync(job, "bulk-job-unknown-kind");
            return;
        }
        job.Status = BulkJobStatus.RUNNING;
        job.StartedAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        var done = (JsonSerializer.Deserialize<List<string>>(job.DoneIds) ?? []).ToHashSet();
        var failures = JsonSerializer.Deserialize<List<BulkJobFailureDto>>(job.Failures, JsonSerializerOptions.Web) ?? [];
        var result = string.IsNullOrEmpty(job.Result) ? [] : JsonSerializer.Deserialize<Dictionary<string, long>>(job.Result) ?? [];
        try
        {
            var remaining = all.Where(id => !done.Contains(id)).ToList();
            for (var at = 0; at < remaining.Count; at += BulkJobKinds.ChunkSize)
            {
                var chunk = remaining.Skip(at).Take(BulkJobKinds.ChunkSize).ToList();
                await RunChunkAsync(job, input.RootElement, chunk, result, failures, cancellationToken);
                foreach (var id in chunk) done.Add(id);
                job.Processed = done.Count;
                job.DoneIds = JsonSerializer.Serialize(done);
                job.Failures = JsonSerializer.Serialize(failures, JsonSerializerOptions.Web);
                job.Result = JsonSerializer.Serialize(result);
                await db.SaveChangesAsync(cancellationToken);
            }
            var final = await FinishAsync(job, input.RootElement, result, cancellationToken);
            job.Status = BulkJobStatus.DONE;
            job.Processed = done.Count;
            job.Result = JsonSerializer.Serialize(final);
            job.FinishedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning("Tâche de masse {Id} ({Kind} {Target}) arrêtée : {Error}", job.Id, job.Kind, job.Target, error.Message);
            db.ChangeTracker.Clear();
            var row = await db.BulkJobs.FirstAsync(j => j.Id == job.Id, CancellationToken.None);
            row.Status = BulkJobStatus.FAILED;
            row.Error = ReasonOf(error);
            row.FinishedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }

    private async Task RunChunkAsync(BulkJob job, JsonElement input, List<string> chunk, Dictionary<string, long> result, List<BulkJobFailureDto> failures, CancellationToken cancellationToken)
    {
        try
        {
            Add(result, await ChunkAsync(job, input, chunk, cancellationToken));
            return;
        }
        catch (BusinessException error) when (IsParcelError(error) && chunk.Count > 1)
        {
            db.ChangeTracker.Clear();
        }
        catch (BusinessException error) when (IsParcelError(error))
        {
            db.ChangeTracker.Clear();
            failures.Add(await FailureAsync(chunk[0], error, cancellationToken));
            return;
        }
        foreach (var id in chunk)
        {
            try
            {
                Add(result, await ChunkAsync(job, input, [id], cancellationToken));
            }
            catch (BusinessException error) when (IsParcelError(error))
            {
                db.ChangeTracker.Clear();
                failures.Add(await FailureAsync(id, error, cancellationToken));
            }
        }
    }

    private static IReadOnlyList<string>? Kept(JsonElement input) =>
        input.TryGetProperty("kept", out var kept) && kept.ValueKind == JsonValueKind.Array ? kept.EnumerateArray().Select(e => e.GetString()!).ToList() : null;

    private static string? Text(JsonElement input, string key) => input.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int Int(JsonElement input, string key) => input.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;

    private async Task<Dictionary<string, long>> ChunkAsync(BulkJob job, JsonElement input, List<string> chunk, CancellationToken cancellationToken)
    {
        switch (job.Kind)
        {
            case BulkJobKinds.NoteValidate:
                var part = await NoteValidation.RunAsync(db, caller, logger, job.Target, Kept(input), new NoteValidation.Options(ParcelsPhase: true, ChunkIds: chunk, PickedUpCount: Int(input, "pickedUpCount")), cancellationToken);
                return new() { ["removed"] = part.Removed, ["validated"] = part.Validated };
            case BulkJobKinds.RouteDispatch:
                return new() { ["count"] = await RouteDispatch.RunAsync(db, caller, logger, job.Target, new RouteDispatch.Options(ParcelsPhase: true, ChunkIds: chunk), cancellationToken) };
            case BulkJobKinds.RouteOutcomes:
                var outcomes = (RouteBulkConfirmResultDto)await RouteBulkOutcomes.RunAsync(db, caller, logger, null, new ConfirmRouteOutcomesCommand
                {
                    Code = job.Target, FromJob = true, ParcelIds = chunk, StatusId = input.TryGetProperty("statusId", out var status) ? status.Clone() : null,
                    ScheduledAt = Text(input, "scheduledAt"), Comment = Text(input, "comment"),
                }, cancellationToken);
                return new() { ["count"] = outcomes.Count, ["cashExpected"] = outcomes.CashExpected };
            case BulkJobKinds.ParcelTransfer:
                var moved = (ParcelTransferResultDto)await ParcelTransfers.RunAsync(db, caller, clock, logger, null, new TransferParcelsCommand
                {
                    FromJob = true, ParcelIds = chunk, CourierId = Text(input, "courierId"), Reason = Text(input, "reason"), DestinationCityId = Text(input, "destinationCityId"),
                }, new ParcelTransfers.Options(), cancellationToken);
                return new() { ["count"] = moved.Count, ["skipped"] = moved.Skipped };
            default:
                throw new InvalidOperationException($"Tâche inconnue : {job.Kind}");
        }
    }

    private async Task<Dictionary<string, long>> FinishAsync(BulkJob job, JsonElement input, Dictionary<string, long> result, CancellationToken cancellationToken)
    {
        switch (job.Kind)
        {
            case BulkJobKinds.NoteValidate:
                var removed = result.GetValueOrDefault("removed");
                await NoteValidation.RunAsync(db, caller, logger, job.Target, Kept(input), new NoteValidation.Options(FinishPhase: true, PickedUpCount: Int(input, "pickedUpCount"), RemovedTotal: (int)removed), cancellationToken);
                return new(result) { ["count"] = result.GetValueOrDefault("validated"), ["removed"] = removed };
            case BulkJobKinds.RouteDispatch:
                await RouteDispatch.RunAsync(db, caller, logger, job.Target, new RouteDispatch.Options(FinishPhase: true), cancellationToken);
                return result;
            default:
                return result;
        }
    }

    private static void Add(Dictionary<string, long> into, Dictionary<string, long>? part)
    {
        if (part is null) return;
        foreach (var (key, value) in part) into[key] = into.GetValueOrDefault(key) + value;
    }

    private static bool IsParcelError(BusinessException error) => error.Code is ErrorCode.CONFLICT or ErrorCode.VALIDATION_FAILED or ErrorCode.TRANSITION_FORBIDDEN;

    private async Task<BulkJobFailureDto> FailureAsync(string parcelId, Exception error, CancellationToken cancellationToken)
    {
        var code = await db.Parcels.AsNoTracking().Where(p => p.Id == parcelId).Select(p => p.Code).FirstOrDefaultAsync(cancellationToken);
        return new BulkJobFailureDto(parcelId, code, ReasonOf(error));
    }

    private static string ReasonOf(Exception error)
    {
        if (error is not BusinessException business) return "error";
        var reason = business.Details?.GetType().GetProperty("reason")?.GetValue(business.Details) as string;
        return reason ?? business.Code.ToString();
    }

    private async Task StopAsync(BulkJob job, string error)
    {
        job.Status = BulkJobStatus.FAILED;
        job.Error = error;
        job.FinishedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(CancellationToken.None);
    }
}
