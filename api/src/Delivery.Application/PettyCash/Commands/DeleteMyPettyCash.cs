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

/// <summary>`DELETE /courier/me/petty-cash/:id` (204) : un brouillon seulement.</summary>
public sealed record DeleteMyPettyCashCommand(string Id) : IRequest<Unit>;

internal sealed class DeleteMyPettyCashHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<DeleteMyPettyCashCommand, Unit>
{
    public async Task<Unit> Handle(DeleteMyPettyCashCommand request, CancellationToken cancellationToken)
    {
        var current = await CourierExpenses.DraftAsync(db, caller, request.Id, cancellationToken);
        await PettyCashDays.WriteAsync(db, clock, [(current.AgencyId, PettyCashDays.DayOf(current.OccurredAt))], async () =>
        {
            await CourierExpenses.AssertStillDraftAsync(db, current.Id, cancellationToken);
            return await db.PettyCashMovements.Where(m => m.Id == current.Id).ExecuteDeleteAsync(cancellationToken);
        }, cancellationToken, draftOnly: true);
        return Unit.Value;
    }
}
