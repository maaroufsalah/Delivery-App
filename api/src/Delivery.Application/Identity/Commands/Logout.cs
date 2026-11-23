using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using FluentValidation;
using MediatR;

namespace Delivery.Application.Identity.Commands;

/// <summary>
/// `POST /auth/logout` : identifie le compte par le refresh puis l'accès
/// (expiration ignorée) et révoque TOUS ses jetons. Toujours 204.
/// </summary>
public sealed record LogoutCommand(string? AccessToken, string? CookieToken, string? BodyToken) : IRequest
{
    public string? BodyToken { get; init; } = Zod.Trim(BodyToken);
}

/// <summary>`refreshSchema` : `refreshToken` facultatif, non vide une fois rogné.</summary>
public sealed class LogoutValidator : ZodValidator<LogoutCommand>
{
    public LogoutValidator()
    {
        RuleFor(x => x.BodyToken).MinLength(1).OverridePropertyName("refreshToken");
    }
}

internal sealed class LogoutHandler(IAppDbContext db, IJwtService jwt, IClock clock) : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        string? userId = null;
        foreach (var (token, kind) in new[] { (request.CookieToken ?? request.BodyToken, JwtKind.Refresh), (request.AccessToken, JwtKind.Access) })
        {
            if (string.IsNullOrEmpty(token)) continue;
            var payload = jwt.Verify(kind, token, ignoreExpiration: true);
            if (!string.IsNullOrEmpty(payload?.Sub))
            {
                userId = payload.Sub;
                break;
            }
        }
        if (userId is null) return;
        try
        {
            await Sessions.RevokeAllAsync(db, clock, userId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // `revokeAll(...).catch(() => undefined)` : la déconnexion n'échoue jamais.
        }
    }
}
