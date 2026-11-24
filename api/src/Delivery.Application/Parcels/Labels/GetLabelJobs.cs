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

/// <summary>`GET /parcels/label-jobs` : mes travaux encore valables (20 au plus).</summary>
public sealed record GetLabelJobsQuery : IRequest<IReadOnlyList<LabelJobDto>>;

internal sealed class GetLabelJobsHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<GetLabelJobsQuery, IReadOnlyList<LabelJobDto>>
{
    public async Task<IReadOnlyList<LabelJobDto>> Handle(GetLabelJobsQuery request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var rows = await db.LabelJobs.AsNoTracking()
            .Where(j => j.RequestedById == caller.UserId && j.ExpiresAt > now && ParcelLabels.Formats.Contains(j.Format))
            .OrderByDescending(j => j.CreatedAt).Take(20).ToListAsync(cancellationToken);
        return rows.Select(LabelJobDto.From).ToList();
    }
}
