using AutoMapper;
using AutoMapper.QueryableExtensions;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Users.Dtos;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Users.Queries;

/// <summary>`GET /users` : comptes de la société, filtrés, triés et paginés côté serveur.</summary>
public sealed record GetUsersQuery(ListQuery Query, IReadOnlyList<string>? Roles, IReadOnlyList<string>? AgencyIds) : IRequest<Paginated<UserListItemDto>>;

internal sealed class GetUsersHandler(IAppDbContext db, IMapper mapper) : IRequestHandler<GetUsersQuery, Paginated<UserListItemDto>>
{
    internal static readonly IReadOnlyDictionary<string, Func<IQueryable<User>, bool, IOrderedQueryable<User>>> Sorts =
        new Dictionary<string, Func<IQueryable<User>, bool, IOrderedQueryable<User>>>
        {
            ["lastName"] = (q, d) => d ? q.OrderByDescending(u => u.LastName) : q.OrderBy(u => u.LastName),
            ["email"] = (q, d) => d ? q.OrderByDescending(u => u.Email) : q.OrderBy(u => u.Email),
            ["agencyName"] = (q, d) => d ? q.OrderByDescending(u => u.Agency!.Name) : q.OrderBy(u => u.Agency!.Name),
            ["lastLoginAt"] = (q, d) => d ? q.OrderByDescending(u => u.LastLoginAt) : q.OrderBy(u => u.LastLoginAt),
            ["createdAt"] = (q, d) => d ? q.OrderByDescending(u => u.CreatedAt) : q.OrderBy(u => u.CreatedAt),
            ["isActive"] = (q, d) => d ? q.OrderByDescending(u => u.IsActive) : q.OrderBy(u => u.IsActive),
        };

    public async Task<Paginated<UserListItemDto>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var where = Filter(db.Users.AsNoTracking(), request);
        var ordered = Sorting.OrderByKey(where, request.Query, Sorts, "lastName");
        var total = await where.CountAsync(cancellationToken);
        var items = await ordered.Skip(request.Query.Skip).Take(request.Query.PageSize)
            .ProjectTo<UserListItemDto>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
        return Paginated<UserListItemDto>.Of(items, total, request.Query);
    }

    internal static IQueryable<User> Filter(IQueryable<User> users, GetUsersQuery request)
    {
        var query = users.Where(u => u.DeletedAt == null);
        if (request.Query.ActiveFilter is { } active) query = query.Where(u => u.IsActive == active);
        if (request.Roles is { Count: > 0 } roleNames)
        {
            // Une valeur hors énumération fait échouer Prisma (erreur de validation, donc 500) : reproduit.
            var roles = roleNames.Select(r => Enum.TryParse<RoleName>(r, out var parsed) && Enum.IsDefined(parsed) && !int.TryParse(r, out _)
                ? parsed
                : throw new InvalidOperationException($"Rôle inconnu : {r}")).ToList();
            query = query.Where(u => u.Roles.Any(r => roles.Contains(r.Role)));
        }
        if (request.AgencyIds is { Count: > 0 } agencyIds)
        {
            query = query.Where(u => (u.AgencyId != null && agencyIds.Contains(u.AgencyId)) || u.Agencies.Any(a => agencyIds.Contains(a.AgencyId)));
        }
        return Search(query, request.Query.Search);
    }

    internal static IQueryable<User> Search(IQueryable<User> query, string? search)
    {
        if (string.IsNullOrEmpty(search)) return query;
        var pattern = Like.Contains(search);
        return query.Where(u => EF.Functions.ILike(u.FirstName, pattern) || EF.Functions.ILike(u.LastName, pattern) || EF.Functions.ILike(u.Email!, pattern));
    }
}
