using System.Text.Json;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.Returns;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Finance;

/// <summary>`POST /remittances/:code/reopen` : l'agence rouvre une caisse fermée par le livreur, s'il n'en a pas ouvert d'autre.</summary>
public sealed record ReopenRemittanceCommand(string Code) : IRequest<RemittanceDetailDto>;

internal sealed class ReopenRemittanceHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<ReopenRemittanceCommand, RemittanceDetailDto>
{
    public async Task<RemittanceDetailDto> Handle(ReopenRemittanceCommand request, CancellationToken cancellationToken)
    {
        if (caller.CourierId is not null) throw BusinessException.Forbidden("Seule l’agence rouvre une caisse.", new { reason = "reopening-is-agency-only" });
        var remittance = await Remittances.Scoped(db.CashRemittances.AsNoTracking(), caller).Where(r => r.Code == request.Code)
                             .Select(r => new { r.Id, r.Status, r.CourierId, r.AgencyId, r.CourierClosedAt }).FirstOrDefaultAsync(cancellationToken)
                         ?? throw BusinessException.NotFound($"Retour de fonds introuvable : {request.Code}");
        if (remittance.Status != CashRemittanceStatus.DRAFT) throw BusinessException.Conflict("Cette caisse est reçue : elle ne se rouvre plus.", new { reason = "remittance-confirmed" });
        if (remittance.CourierClosedAt is null) throw BusinessException.Conflict("Cette caisse est déjà ouverte.", new { reason = "remittance-not-closed" });
        await ReturnRows.AssertAgencyReachableAsync(db, caller, remittance.AgencyId, cancellationToken);
        if (await Remittances.OtherOpenCodeAsync(db, remittance.CourierId, cancellationToken) is { } other) throw Remittances.HasNewerOpen(other);
        int moved;
        try
        {
            moved = await db.CashRemittances.Where(r => r.Id == remittance.Id && r.Status == CashRemittanceStatus.DRAFT && r.CourierClosedAt != null)
                .ExecuteUpdateAsync(set => set.SetProperty(r => r.CourierClosedAt, (DateTime?)null).SetProperty(r => r.CourierClosedById, (string?)null), cancellationToken);
        }
        catch (Npgsql.PostgresException error) when (error.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation)
        {
            throw Remittances.HasNewerOpen(await Remittances.OtherOpenCodeAsync(db, remittance.CourierId, cancellationToken) ?? string.Empty);
        }
        if (moved == 0) throw BusinessException.Conflict("Cette caisse n’est plus fermée.", new { reason = "remittance-not-closed" });
        return await Remittances.DetailAsync(db, caller, request.Code, cancellationToken);
    }
}
