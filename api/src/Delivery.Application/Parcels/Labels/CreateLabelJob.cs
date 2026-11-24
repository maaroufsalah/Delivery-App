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

/// <summary>`POST /parcels/label-jobs` : la planche se prépare en arrière-plan (202).</summary>
public sealed record CreateLabelJobCommand : LabelSelection, IRequest<LabelJobDto>;

public sealed class CreateLabelJobValidator : LabelSelectionValidator<CreateLabelJobCommand>;

internal sealed class CreateLabelJobHandler(IAppDbContext db, ICurrentUser caller, IClock clock, LabelJobQueue queue) : IRequestHandler<CreateLabelJobCommand, LabelJobDto>
{
    public async Task<LabelJobDto> Handle(CreateLabelJobCommand request, CancellationToken cancellationToken)
    {
        var codes = await LabelSelections.CodesAsync(db, caller, clock, request, cancellationToken);
        var now = clock.UtcNow;
        var row = new LabelJob
        {
            RequestedById = caller.UserId,
            Format = request.Format ?? LabelSheet.Format10X10,
            ParcelCount = codes.Count,
            CreatedAt = now,
            ExpiresAt = now.AddHours(ParcelLabels.JobTtlHours),
        };
        db.LabelJobs.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        queue.Enqueue(new LabelJobWork(row.Id, codes, row.Format, $"labels/{caller.CompanyId}"));
        return LabelJobDto.From(row);
    }
}
