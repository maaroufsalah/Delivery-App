using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.Identity.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Identity;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Identity.Commands;

/// <summary>`POST /auth/login` : identifiant (e-mail ou nom d'utilisateur) et mot de passe.</summary>
public sealed record LoginCommand : IRequest<AuthResult>
{
    public string? Identifier { get; init => field = Zod.Trim(value); }

    public string? Email { get; init => field = Zod.Trim(value); }

    public string? Password { get; init; }

    public bool? Remember { get; init; }

    public string Login => Identifier ?? Email ?? string.Empty;
}

public sealed class LoginValidator : ZodValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Identifier).MinLength(1, "required").MaxLength(254);
        RuleFor(x => x.Email).MinLength(1, "required").MaxLength(254);
        RuleFor(x => x.Password).Required().MinLength(1, "required");
    }

    protected override void Refine(LoginCommand value, Action<string, string> issue)
    {
        if (string.IsNullOrEmpty(value.Identifier ?? value.Email)) issue("identifier", "required");
    }
}

internal sealed class LoginHandler(
    IAppDbContext db, IJwtService jwt, IClock clock, ICurrentUser caller, LoginThrottle throttle, ILogger<LoginHandler> logger)
    : IRequestHandler<LoginCommand, AuthResult>
{
    // Empreinte factice : un identifiant inconnu coûte le même temps qu'un mot de passe faux.
    private const string Placeholder = "$argon2id$v=19$m=19456,t=2,p=1$c2FsdHNhbHRzYWx0c2FsdA$P1k1nFqCkPZMr7z9r9G9r7z9r9G9r7z9r9G9r7z9r9G";

    public async Task<AuthResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var address = caller.IpAddress ?? "unknown";
        throttle.AssertAllowed(address, request.Login, clock.UtcNow);
        var kind = caller.IsMobileClient ? SessionPolicy.Mobile : request.Remember == true ? SessionPolicy.Remember : SessionPolicy.Web;

        IssuedSession session;
        try
        {
            session = await LoginAsync(request.Login, request.Password!, kind, cancellationToken);
        }
        catch (BusinessException error) when (IsCredentialFailure(error))
        {
            throttle.RecordFailure(address, request.Login, clock.UtcNow);
            logger.LogWarning("échec de connexion — adresse {Address}", address);
            await JournalAsync(false, null, request.Login, cancellationToken);
            throw;
        }

        logger.LogInformation("connexion — adresse {Address}", address);
        await JournalAsync(true, session.User.UserId, request.Login, cancellationToken);
        return new AuthResult(session, caller.IsMobileClient);
    }

    internal static bool IsCredentialFailure(BusinessException error) =>
        error.Code == ErrorCode.UNAUTHENTICATED && !ReasonIs(error.Details, "pending-validation");

    private static bool ReasonIs(object? details, string reason) =>
        details?.GetType().GetProperty("reason")?.GetValue(details) as string == reason;

    private async Task<IssuedSession> LoginAsync(string login, string password, string kind, CancellationToken cancellationToken)
    {
        var identifier = login.Trim().ToLowerInvariant();
        var byEmail = identifier.Contains('@', StringComparison.Ordinal);
        var user = await db.Users
            .Include(u => u.Roles)
            .Include(u => u.Client)
            .Include(u => u.Courier)
            .Where(u => u.DeletedAt == null && (byEmail ? u.Email == identifier : u.Username == identifier))
            .FirstOrDefaultAsync(cancellationToken);
        var invalid = BusinessException.Unauthenticated("Identifiants incorrects.");
        if (user is null)
        {
            PasswordHasher.Verify(Placeholder, "x");
            throw invalid;
        }
        if (!PasswordHasher.Verify(user.PasswordHash, password)) throw invalid;
        if (user.Status == UserStatus.PENDING)
        {
            throw BusinessException.Unauthenticated("Votre demande est envoyée, un administrateur doit la valider.", new { reason = "pending-validation" });
        }
        if (!user.IsActive) throw invalid;

        var companyId = await Sessions.CompanyIdAsync(db, cancellationToken);
        user.LastLoginAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return await Sessions.IssueAsync(db, jwt, clock, Sessions.IdentityOf(user, companyId), kind, false, null, cancellationToken);
    }

    /// <summary>`LoginJournalService.record` : une ligne d'audit par tentative, jamais bloquante.</summary>
    private async Task JournalAsync(bool success, string? userId, string identifier, CancellationToken cancellationToken)
    {
        try
        {
            var needle = identifier.Trim().ToLowerInvariant();
            var query = db.Users.AsNoTracking().Select(u => new { u.Id, u.Email, u.Username, u.DeletedAt, Role = u.Roles.Select(r => (RoleName?)r.Role).FirstOrDefault() });
            var user = userId is not null
                ? await query.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
                : needle.Length == 0
                    ? null
                    : await query.FirstOrDefaultAsync(u => u.DeletedAt == null && (needle.Contains('@') ? u.Email == needle : u.Username == needle), cancellationToken);
            db.AuditLogs.Add(new AuditLog
            {
                Entity = "User",
                EntityId = user?.Id ?? string.Empty,
                Action = success ? AuditAction.LOGIN : AuditAction.LOGIN_FAILED,
                Diff = [],
                UserId = success ? user?.Id : null,
                UserEmail = success ? user?.Email ?? user?.Username : null,
                Role = success ? user?.Role?.ToString() : null,
                Ip = caller.IpAddress,
                UserAgent = caller.UserAgent,
                RequestId = caller.RequestId,
                Source = caller.IsMobileClient ? "mobile" : "web",
                After = user is null ? "{\"unknownAccount\":true}" : null,
                At = clock.UtcNow,
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is DbUpdateException or InvalidOperationException)
        {
            logger.LogWarning("connexion non journalisée : {Message}", exception.Message);
        }
    }
}
