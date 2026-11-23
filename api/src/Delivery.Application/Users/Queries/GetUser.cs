using AutoMapper;
using AutoMapper.QueryableExtensions;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Users.Dtos;
using Delivery.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Users.Queries;

/// <summary>`GET /users/:id`.</summary>
public sealed record GetUserQuery(string Id) : IRequest<UserDetailDto>;

internal sealed class GetUserHandler(IAppDbContext db, IMapper mapper) : IRequestHandler<GetUserQuery, UserDetailDto>
{
    public Task<UserDetailDto> Handle(GetUserQuery request, CancellationToken cancellationToken) => LoadAsync(db, mapper, request.Id, cancellationToken);

    internal static async Task<UserDetailDto> LoadAsync(IAppDbContext db, IMapper mapper, string id, CancellationToken cancellationToken) =>
        await db.Users.AsNoTracking()
            .Where(u => u.Id == id && u.DeletedAt == null)
            .ProjectTo<UserDetailDto>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw BusinessException.NotFound("Compte introuvable.");

    internal static async Task<UserListItemDto> ListItemAsync(IAppDbContext db, IMapper mapper, string id, CancellationToken cancellationToken) =>
        await db.Users.AsNoTracking()
            .Where(u => u.Id == id)
            .ProjectTo<UserListItemDto>(mapper.ConfigurationProvider)
            .FirstAsync(cancellationToken);
}
