using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Idempotency;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.PettyCash;

/// <summary>`PettyCashDaysService.sweep` : la tâche de fond qui clôt les journées écoulées.</summary>
public sealed record SweepPettyCashDaysCommand : IRequest<PettyCashSweepDto>;

internal sealed class SweepPettyCashDaysHandler(IAppDbContext db, IClock clock) : IRequestHandler<SweepPettyCashDaysCommand, PettyCashSweepDto>
{
    public async Task<PettyCashSweepDto> Handle(SweepPettyCashDaysCommand request, CancellationToken cancellationToken)
    {
        var (created, closed) = await PettyCashDays.SweepAsync(db, clock, cancellationToken);
        return new PettyCashSweepDto(created, closed);
    }
}
