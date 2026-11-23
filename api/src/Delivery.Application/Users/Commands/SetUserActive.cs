using AutoMapper;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Application.Identity;
using Delivery.Application.Users.Dtos;
using Delivery.Application.Users.Queries;
using Delivery.Domain.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Users.Commands;

/// <summary>`PATCH /users/:id/active` : désactiver ferme aussitôt les sessions du compte.</summary>
public sealed record SetUserActiveCommand : IRequest<UserDetailDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Id { get; init; } = string.Empty;

    public bool? IsActive { get; init; }
}

public sealed class SetUserActiveValidator : ZodValidator<SetUserActiveCommand>
{
    public SetUserActiveValidator() => RuleFor(x => x.IsActive).Required("boolean");
}

internal sealed class SetUserActiveHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IMapper mapper) : IRequestHandler<SetUserActiveCommand, UserDetailDto>
{
    public async Task<UserDetailDto> Handle(SetUserActiveCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.Id && u.DeletedAt == null, cancellationToken)
            ?? throw BusinessException.NotFound("Compte introuvable.");
        var isActive = request.IsActive!.Value;
        if (!isActive && request.Id == caller.UserId)
        {
            throw BusinessException.Validation("Un compte ne peut pas se désactiver lui-même.", new { field = "isActive" });
        }
        if (!isActive) await UserFields.AssertNotLastSuperAdminAsync(db, request.Id, cancellationToken);
        var now = clock.UtcNow;
        if (isActive) user.IsActive = true;
        else user.Deactivate(now);
        await db.SaveChangesAsync(cancellationToken);
        if (!isActive) await Sessions.CloseAsync(db, user.Id, now, cancellationToken);
        return await GetUserHandler.LoadAsync(db, mapper, user.Id, cancellationToken);
    }
}
