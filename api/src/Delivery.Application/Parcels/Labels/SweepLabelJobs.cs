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

/// <summary>Ménage des travaux : au démarrage, les travaux interrompus ; ensuite, les expirés et leurs fichiers.</summary>
public sealed record SweepLabelJobsCommand(bool Recover) : IRequest<int>;

internal sealed class SweepLabelJobsHandler(IAppDbContext db, IFileStorage storage, IClock clock) : IRequestHandler<SweepLabelJobsCommand, int>
{
    public async Task<int> Handle(SweepLabelJobsCommand request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        if (request.Recover)
        {
            await db.LabelJobs.Where(j => j.Status == LabelJobStatus.PENDING || j.Status == LabelJobStatus.RUNNING)
                .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, LabelJobStatus.FAILED).SetProperty(j => j.Error, "interrupted").SetProperty(j => j.FinishedAt, now), cancellationToken);
        }
        var expired = await db.LabelJobs.Where(j => j.ExpiresAt <= now).ToListAsync(cancellationToken);
        foreach (var row in expired.Where(r => r.StorageKey is not null)) await storage.RemoveAsync(row.StorageKey!, cancellationToken);
        db.LabelJobs.RemoveRange(expired);
        await db.SaveChangesAsync(cancellationToken);
        return expired.Count;
    }
}
