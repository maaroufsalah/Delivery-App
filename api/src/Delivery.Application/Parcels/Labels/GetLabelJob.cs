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

/// <summary>`GET /parcels/label-jobs/:id`.</summary>
public sealed record GetLabelJobQuery(string Id) : IRequest<LabelJobDto>;

internal sealed class GetLabelJobHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<GetLabelJobQuery, LabelJobDto>
{
    public async Task<LabelJobDto> Handle(GetLabelJobQuery request, CancellationToken cancellationToken) =>
        LabelJobDto.From(await LabelJobFiles.OwnAsync(db, caller, clock, request.Id, cancellationToken));
}
