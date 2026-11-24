using System.Text.Json;
using System.Threading.Channels;
using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.BulkJobs;

/// <summary>`GET /bulk-jobs/:id`.</summary>
public sealed record GetBulkJobQuery(string Id) : IRequest<BulkJobDto>;

internal sealed class GetBulkJobHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetBulkJobQuery, BulkJobDto>
{
    public async Task<BulkJobDto> Handle(GetBulkJobQuery request, CancellationToken cancellationToken) =>
        BulkJobDto.From(await BulkJobWrites.Visible(db.BulkJobs.AsNoTracking(), caller).FirstOrDefaultAsync(j => j.Id == request.Id, cancellationToken)
                        ?? throw BusinessException.NotFound("Tâche introuvable."));
}
