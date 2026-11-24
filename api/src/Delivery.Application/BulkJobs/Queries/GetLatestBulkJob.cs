using System.Text.Json;
using System.Threading.Channels;
using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.BulkJobs;

/// <summary>`GET /bulk-jobs?target=` : la dernière tâche de cet objet, ou `null`.</summary>
public sealed record GetLatestBulkJobQuery(string? Target) : IRequest<BulkJobDto?>;

internal sealed class GetLatestBulkJobHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetLatestBulkJobQuery, BulkJobDto?>
{
    public async Task<BulkJobDto?> Handle(GetLatestBulkJobQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Target)) return null;
        var target = request.Target.Length > 64 ? request.Target[..64] : request.Target;
        var row = await BulkJobWrites.Visible(db.BulkJobs.AsNoTracking(), caller).Where(j => j.Target == target).OrderByDescending(j => j.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : BulkJobDto.From(row);
    }
}
