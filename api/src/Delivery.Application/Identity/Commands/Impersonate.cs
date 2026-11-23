using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Application.Identity.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Identity;
using FluentValidation;
using MediatR;

namespace Delivery.Application.Identity.Commands;

/// <summary>`POST /auth/impersonate` : échange d'un jeton d'accès console (2 min) contre une session.</summary>
public sealed record ImpersonateCommand : IRequest<AuthResult>
{
    public string? Token { get; init => field = Zod.Trim(value); }
}

public sealed class ImpersonateValidator : ZodValidator<ImpersonateCommand>
{
    public ImpersonateValidator()
    {
        RuleFor(x => x.Token).Required().MinLength(20, "too-short").MaxLength(4096);
    }
}

internal sealed class ImpersonateHandler(IAppDbContext db, IJwtService jwt, IClock clock, ICurrentUser caller)
    : IRequestHandler<ImpersonateCommand, AuthResult>
{
    public async Task<AuthResult> Handle(ImpersonateCommand request, CancellationToken cancellationToken)
    {
        var companyId = await Sessions.CompanyIdAsync(db, cancellationToken) ?? throw BusinessException.NotFound("Espace inconnu.");
        var payload = jwt.Verify(JwtKind.Access, request.Token!)
            ?? throw BusinessException.Unauthenticated("Lien d’accès expiré. Relancez depuis la console.");
        if (payload.Purpose != "impersonate") throw BusinessException.Unauthenticated("Jeton invalide.");
        if (payload.Cid != companyId)
        {
            throw BusinessException.Forbidden("Ce lien d’accès ne concerne pas cet espace.", new { reason = "tenant-mismatch" });
        }
        var user = await Sessions.LoadUserAsync(db, payload.Sub, cancellationToken);
        if (user is null || !user.IsActive) throw BusinessException.Unauthenticated("Compte inactif.");
        var session = await Sessions.IssueAsync(db, jwt, clock, Sessions.IdentityOf(user, companyId, mustChangePassword: false), SessionPolicy.Web, true, null, cancellationToken);
        return new AuthResult(session, caller.IsMobileClient);
    }
}
