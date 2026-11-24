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

/// <summary>`GET /settings/providers/:id/calls` : les 50 derniers appels et toutes les alertes ouvertes.</summary>
public sealed record GetProviderCallsQuery(string Id) : IRequest<IReadOnlyList<ProviderCallListItemDto>>;

internal sealed class GetProviderCallsHandler(IAppDbContext db) : IRequestHandler<GetProviderCallsQuery, IReadOnlyList<ProviderCallListItemDto>>
{
    public async Task<IReadOnlyList<ProviderCallListItemDto>> Handle(GetProviderCallsQuery request, CancellationToken cancellationToken)
    {
        var token = await ProviderTokens.FindAsync(db, request.Id, cancellationToken);
        var calls = db.ProviderCalls.AsNoTracking().Where(c => c.TokenId == token.Id);
        var recent = await calls.OrderByDescending(c => c.At).Take(ProviderTokens.CallsLimit).ToListAsync(cancellationToken);
        var open = await calls.Where(c => c.Alert && c.AlertResolvedAt == null).OrderByDescending(c => c.At).ToListAsync(cancellationToken);
        var seen = recent.Select(c => c.Id).ToHashSet();
        return recent.Concat(open.Where(c => !seen.Contains(c.Id))).Select(c => new ProviderCallListItemDto(c.Id, c.At, c.EventKey, c.ParcelCode, c.ProviderStatus, c.MappedStatus,
            c.OccurredAt, c.Mode.ToString(), c.Outcome.ToString(), c.HttpStatus, c.Reason, c.Alert, c.AlertResolvedAt)).ToList();
    }
}
