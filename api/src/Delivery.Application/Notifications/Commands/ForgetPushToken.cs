using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Notifications;

/// <summary>`forgetTokenSchema` : à la déconnexion, l'appareil de CE compte est oublié.</summary>
public sealed record ForgetPushTokenCommand : IRequest<RemovedDto>
{
    public string? Token { get; init => field = Zod.Trim(value); }
}

public sealed class ForgetPushTokenValidator : ZodValidator<ForgetPushTokenCommand>
{
    public ForgetPushTokenValidator() => RuleFor(x => x.Token).Required().MinLength(1, "required");
}

internal sealed class ForgetPushTokenHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<ForgetPushTokenCommand, RemovedDto>
{
    public async Task<RemovedDto> Handle(ForgetPushTokenCommand input, CancellationToken cancellationToken) =>
        new(await db.PushTokens.Where(t => t.Token == input.Token && t.UserId == caller.UserId).ExecuteDeleteAsync(cancellationToken));
}
