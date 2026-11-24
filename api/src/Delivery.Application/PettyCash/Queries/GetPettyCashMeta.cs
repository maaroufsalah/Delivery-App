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

/// <summary>`GET /petty-cash/meta` : catégories, acheteurs possibles et agences.</summary>
public sealed record GetPettyCashMetaQuery : IRequest<PettyCashMetaDto>;

internal sealed class GetPettyCashMetaHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetPettyCashMetaQuery, PettyCashMetaDto>
{
    public async Task<PettyCashMetaDto> Handle(GetPettyCashMetaQuery request, CancellationToken cancellationToken)
    {
        var buyers = await PettyCashMovements.Buyers(db, caller).OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Select(u => new PettyCashPersonDto(u.Id, u.FirstName + " " + u.LastName)).ToListAsync(cancellationToken);
        var agencies = db.Agencies.AsNoTracking();
        if (!caller.IsSuperAdmin()) { var own = caller.AgencyId ?? Scope.DenyAllId; agencies = agencies.Where(a => a.Id == own); }
        return new PettyCashMetaDto(await PettyCashMovements.CategoriesAsync(db, cancellationToken), buyers,
            await agencies.OrderBy(a => a.Name).Select(a => new PettyCashAgencyDto(a.Id, a.Name)).ToListAsync(cancellationToken));
    }
}
