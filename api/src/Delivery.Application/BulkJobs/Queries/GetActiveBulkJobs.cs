using System.Text.Json;
using System.Threading.Channels;
using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.BulkJobs;

/// <summary>Les tâches restées actives, à remettre dans la file au démarrage (`resume`).</summary>
public sealed record GetActiveBulkJobsQuery : IRequest<IReadOnlyList<string>>;

internal sealed class GetActiveBulkJobsHandler(IAppDbContext db) : IRequestHandler<GetActiveBulkJobsQuery, IReadOnlyList<string>>
{
    public async Task<IReadOnlyList<string>> Handle(GetActiveBulkJobsQuery request, CancellationToken cancellationToken) =>
        await db.BulkJobs.AsNoTracking().Where(j => j.Status == BulkJobStatus.PENDING || j.Status == BulkJobStatus.RUNNING).OrderBy(j => j.CreatedAt).Select(j => j.Id).ToListAsync(cancellationToken);
}
