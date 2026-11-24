using System.Text.Json;
using System.Threading.Channels;
using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.BulkJobs;

/// <summary>`POST /bulk-jobs/:id/retry` : une tâche arrêtée reprend où elle s'était arrêtée.</summary>
public sealed record RetryBulkJobCommand(string Id) : IRequest<BulkJobDto>;

internal sealed class RetryBulkJobHandler(IAppDbContext db, ICurrentUser caller, BulkJobQueue queue) : IRequestHandler<RetryBulkJobCommand, BulkJobDto>
{
    public async Task<BulkJobDto> Handle(RetryBulkJobCommand request, CancellationToken cancellationToken)
    {
        var row = await BulkJobWrites.Visible(db.BulkJobs, caller).FirstOrDefaultAsync(j => j.Id == request.Id, cancellationToken) ?? throw BusinessException.NotFound("Tâche introuvable.");
        if (row.Status != BulkJobStatus.FAILED) throw BusinessException.Conflict("Seule une tâche arrêtée se relance.", new { reason = "bulk-job-not-failed" });
        await BulkJobWrites.AssertIdleAsync(db, row.Target, cancellationToken);
        row.Status = BulkJobStatus.PENDING;
        row.Error = null;
        row.FinishedAt = null;
        await db.SaveChangesAsync(cancellationToken);
        queue.Enqueue(row.Id);
        return BulkJobDto.From(row);
    }
}
