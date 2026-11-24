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

/// <summary>`GET /settings/providers`.</summary>
public sealed record GetProviderTokensQuery : IRequest<IReadOnlyList<ProviderTokenListItemDto>>;

internal sealed class GetProviderTokensHandler(IAppDbContext db) : IRequestHandler<GetProviderTokensQuery, IReadOnlyList<ProviderTokenListItemDto>>
{
    public async Task<IReadOnlyList<ProviderTokenListItemDto>> Handle(GetProviderTokensQuery request, CancellationToken cancellationToken)
    {
        var rows = await db.ProviderTokens.AsNoTracking().OrderByDescending(t => t.CreatedAt)
            .Select(t => new { Token = t, t.Courier.FirstName, t.Courier.LastName, Alerts = t.Calls.Count(c => c.Alert && c.AlertResolvedAt == null) })
            .ToListAsync(cancellationToken);
        return rows.Select(r => ProviderTokens.ToItem(r.Token, r.FirstName, r.LastName, r.Alerts)).ToList();
    }
}
