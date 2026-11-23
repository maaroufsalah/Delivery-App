using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.Identity;
using Delivery.Application.Users.Dtos;
using Delivery.Domain.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Users.Commands;

/// <summary>`POST /users/me/password` : seule route ouverte avec un mot de passe provisoire.</summary>
public sealed record ChangeOwnPasswordCommand : IRequest<OkResult>
{
    public string? CurrentPassword { get; init; }

    public string? Password { get; init; }

    public string? Confirmation { get; init; }
}

public sealed class ChangeOwnPasswordValidator : ZodValidator<ChangeOwnPasswordCommand>
{
    public ChangeOwnPasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).Required().MinLength(1, "required");
        RuleFor(x => x.Password).Required().MinLength(8, "password-too-short").MaxLength(200);
        RuleFor(x => x.Confirmation).Required().MinLength(1, "required");
    }

    protected override void Refine(ChangeOwnPasswordCommand value, Action<string, string> issue)
    {
        if (value.Password != value.Confirmation) issue("confirmation", "password-mismatch");
        if (value.Password == value.CurrentPassword) issue("password", "password-unchanged");
    }
}

internal sealed class ChangeOwnPasswordHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<ChangeOwnPasswordCommand, OkResult>
{
    public async Task<OkResult> Handle(ChangeOwnPasswordCommand request, CancellationToken cancellationToken)
    {
        caller.EnsureScope();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == caller.UserId && u.DeletedAt == null, cancellationToken)
            ?? throw BusinessException.NotFound("Compte introuvable.");
        if (!PasswordHasher.Verify(user.PasswordHash, request.CurrentPassword!))
        {
            throw BusinessException.Validation("Mot de passe actuel incorrect.", new { field = "currentPassword" });
        }
        var now = clock.UtcNow;
        user.ChangePassword(PasswordHasher.Hash(request.Password!), mustChangePassword: false, now);
        await db.SaveChangesAsync(cancellationToken);
        await Sessions.CloseAsync(db, user.Id, now, cancellationToken);
        return new OkResult(true);
    }
}
