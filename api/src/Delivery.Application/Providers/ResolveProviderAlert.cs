using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Providers;

/// <summary>`POST /settings/providers/:id/calls/:callId/resolve` : l'alerte est traitée.</summary>
public sealed record ResolveProviderAlertCommand(string Id, string CallId) : IRequest<ProviderCallIdDto>;

public sealed record ProviderCallIdDto(string Id);

internal sealed class ResolveProviderAlertHandler(IAppDbContext db, IClock clock) : IRequestHandler<ResolveProviderAlertCommand, ProviderCallIdDto>
{
    public async Task<ProviderCallIdDto> Handle(ResolveProviderAlertCommand request, CancellationToken cancellationToken)
    {
        var token = await ProviderTokens.FindAsync(db, request.Id, cancellationToken);
        var call = await db.ProviderCalls.FirstOrDefaultAsync(c => c.TokenId == token.Id && c.Id == request.CallId && c.Alert, cancellationToken)
                   ?? throw BusinessException.NotFound("Alerte introuvable.", new { reason = "alert-not-found" });
        if (call.AlertResolvedAt is null)
        {
            call.AlertResolvedAt = clock.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        return new ProviderCallIdDto(call.Id);
    }
}
