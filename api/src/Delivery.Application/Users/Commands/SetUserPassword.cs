using AutoMapper;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.Identity;
using Delivery.Application.Users.Dtos;
using Delivery.Application.Users.Queries;
using Delivery.Domain.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Users.Commands;

/// <summary>`POST /users/:id/set-password` : mot de passe choisi par l'administrateur.</summary>
public sealed record SetUserPasswordCommand : IRequest<UserInvitationDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Id { get; init; } = string.Empty;

    public string? Password { get; init; }

    public string? Confirmation { get; init; }

    public bool? MustChangePassword { get; init; }
}

public sealed class SetUserPasswordValidator : ZodValidator<SetUserPasswordCommand>
{
    public SetUserPasswordValidator()
    {
        RuleFor(x => x.Password).Required().MinLength(8, "password-too-short").MaxLength(200);
    }

    protected override void Refine(SetUserPasswordCommand value, Action<string, string> issue)
    {
        if (!string.IsNullOrEmpty(value.Confirmation) && value.Password != value.Confirmation) issue("confirmation", "password-mismatch");
    }
}

internal sealed class SetUserPasswordHandler(IAppDbContext db, IClock clock, IMapper mapper) : IRequestHandler<SetUserPasswordCommand, UserInvitationDto>
{
    public async Task<UserInvitationDto> Handle(SetUserPasswordCommand input, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == input.Id && u.DeletedAt == null, cancellationToken)
            ?? throw BusinessException.NotFound("Compte introuvable.");
        var now = clock.UtcNow;
        user.ChangePassword(PasswordHasher.Hash(input.Password!), input.MustChangePassword ?? false, now);
        await db.SaveChangesAsync(cancellationToken);
        await Sessions.CloseAsync(db, user.Id, now, cancellationToken);
        return new UserInvitationDto(await GetUserHandler.ListItemAsync(db, mapper, user.Id, cancellationToken), input.Password!);
    }
}
