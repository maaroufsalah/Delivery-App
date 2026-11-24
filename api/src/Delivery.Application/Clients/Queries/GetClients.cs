using AutoMapper;
using Delivery.Application.Clients.Dtos;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Clients.Queries;

/// <summary>`GET /clients` : fiches e-commerçants, filtrées par agence et par état de validation.</summary>
public sealed record GetClientsQuery(ListQuery Query, IReadOnlyList<string>? AgencyIds, string? Validated) : IRequest<Paginated<ClientListItemDto>>;

internal sealed class GetClientsHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<GetClientsQuery, Paginated<ClientListItemDto>>
{
    public async Task<Paginated<ClientListItemDto>> Handle(GetClientsQuery request, CancellationToken cancellationToken)
    {
        var where = Filter(db.Clients.AsNoTracking(), request, caller);
        var total = await where.CountAsync(cancellationToken);
        var page = Sorting.OrderByKey(where, request.Query, ClientRows.Sorts, "name").Skip(request.Query.Skip).Take(request.Query.PageSize);
        return Paginated<ClientListItemDto>.Of(await ClientRows.ProjectAsync<ClientListItemDto>(page, mapper, cancellationToken), total, request.Query);
    }

    internal static IQueryable<Client> Filter(IQueryable<Client> clients, GetClientsQuery request, ICurrentUser caller)
    {
        var where = ClientRows.Scoped(clients.Where(c => c.DeletedAt == null), caller);
        if (request.Query.ActiveFilter is { } active) where = where.Where(c => c.IsActive == active);
        if (request.AgencyIds is { Count: > 0 } agencyIds && caller.MayFilterBy(caller.AgencyId)) where = where.Where(c => c.AgencyId != null && agencyIds.Contains(c.AgencyId));
        where = request.Validated switch
        {
            "true" => where.Where(c => c.ValidatedAt != null),
            "false" => where.Where(c => c.ValidatedAt == null && c.RejectedAt == null),
            "rejected" => where.Where(c => c.RejectedAt != null),
            _ => where,
        };
        if (!string.IsNullOrEmpty(request.Query.Search))
        {
            var pattern = Like.Contains(request.Query.Search);
            var raw = request.Query.Search;
            where = where.Where(c => EF.Functions.ILike(c.Name, pattern) || EF.Functions.ILike(c.LegalName!, pattern) || EF.Functions.ILike(c.Email, pattern)
                                     || c.Phone.Contains(raw) || c.TaxId!.Contains(raw));
        }
        return where;
    }
}
