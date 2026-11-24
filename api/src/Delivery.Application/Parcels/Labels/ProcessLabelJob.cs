using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Parcels.Labels;

/// <summary>Le rendu d'un travail de la file (appelé par le service d'arrière-plan).</summary>
public sealed record ProcessLabelJobCommand(LabelJobWork Work) : IRequest;

internal sealed class ProcessLabelJobHandler(IAppDbContext db, IFileStorage storage, IPdfService pdf, IClock clock, ILogger<ProcessLabelJobHandler> logger) : IRequestHandler<ProcessLabelJobCommand>
{
    public async Task Handle(ProcessLabelJobCommand request, CancellationToken cancellationToken)
    {
        var work = request.Work;
        var job = await db.LabelJobs.FirstOrDefaultAsync(j => j.Id == work.JobId, cancellationToken);
        if (job is null) return;
        var startedAt = clock.UtcNow;
        try
        {
            job.Status = LabelJobStatus.RUNNING;
            job.StartedAt = startedAt;
            await db.SaveChangesAsync(cancellationToken);
            var codes = work.Codes;
            var parcels = ParcelLabels.InOrder(await ParcelLabels.LoadAsync(db.Parcels.AsNoTracking().Where(p => codes.Contains(p.Code)), cancellationToken), codes);
            var sheet = await ParcelLabels.SheetAsync(db, storage, parcels, work.Format, cancellationToken);
            var (content, zip) = ParcelLabels.Render(pdf, sheet);
            var stored = await storage.PutAsync(work.Folder, content, zip ? "application/zip" : FileContent.Pdf, cancellationToken);
            var finishedAt = clock.UtcNow;
            job.Status = LabelJobStatus.READY;
            job.StorageKey = stored.Key;
            job.SizeBytes = (int)stored.SizeBytes;
            job.FinishedAt = finishedAt;
            job.ExpiresAt = finishedAt.AddHours(ParcelLabels.JobTtlHours);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Travail {Id} : {Count} lignes en {Ms} ms, {Size} octets.", job.Id, job.ParcelCount, (finishedAt - startedAt).TotalMilliseconds, stored.SizeBytes);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(error, "Étiquettes {Id} : rendu échoué", job.Id);
            job.Status = LabelJobStatus.FAILED;
            job.Error = "failed";
            job.FinishedAt = clock.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }
}
