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

/// <summary>`DELETE /petty-cash/:id` (204).</summary>
public sealed record DeletePettyCashCommand(string Id) : IRequest<Unit>;

internal sealed class DeletePettyCashHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<DeletePettyCashCommand, Unit>
{
    public async Task<Unit> Handle(DeletePettyCashCommand request, CancellationToken cancellationToken)
    {
        var current = await PettyCashMovements.Scoped(db.PettyCashMovements.AsNoTracking(), caller).Where(m => m.Id == request.Id).Select(m => new { m.Id, m.AgencyId, m.OccurredAt })
                          .FirstOrDefaultAsync(cancellationToken) ?? throw BusinessException.NotFound("Mouvement de petite caisse introuvable.");
        await PettyCashDays.WriteAsync(db, clock, [(current.AgencyId, PettyCashDays.DayOf(current.OccurredAt))],
            async () => await db.PettyCashMovements.Where(m => m.Id == current.Id).ExecuteDeleteAsync(cancellationToken), cancellationToken);
        return Unit.Value;
    }
}
