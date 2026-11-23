using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Application.Identity.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Identity.Commands;

/// <summary>
/// `POST /auth/forgot-password` : trace la demande et prévient le titulaire.
/// Le fournisseur d'e-mail est `log` (aucun envoi), comme Speedy aujourd'hui.
/// </summary>
public sealed record ForgotPasswordCommand : IRequest<ForgotPasswordResult>
{
    public string? Email { get; init; }
}

public sealed class ForgotPasswordValidator : ZodValidator<ForgotPasswordCommand>
{
    public ForgotPasswordValidator()
    {
        RuleFor(x => x.Email).Required().EmailAddress("invalid-email");
    }
}

internal sealed class ForgotPasswordHandler(IAppDbContext db, ICurrentUser caller, IClock clock, ILogger<ForgotPasswordHandler> logger)
    : IRequestHandler<ForgotPasswordCommand, ForgotPasswordResult>
{
    public const string MailProvider = "log";

    public async Task<ForgotPasswordResult> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var normalized = request.Email!.Trim().ToLowerInvariant();
        var company = await db.Companies.AsNoTracking().OrderBy(c => c.CreatedAt)
            .Select(c => new { c.Id, c.Name, c.Phone })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw BusinessException.NotFound("Espace inconnu.");
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Email == normalized && u.DeletedAt == null && u.IsActive)
            .Select(u => new { u.Id, u.Email, u.FirstName })
            .FirstOrDefaultAsync(cancellationToken);

        db.PasswordResetRequests.Add(new PasswordResetRequest
        {
            Email = normalized,
            UserId = user?.Id,
            Ip = caller.IpAddress,
            UserAgent = caller.UserAgent,
            MailProvider = MailProvider,
            CreatedAt = clock.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);

        if (user?.Email is not null)
        {
            logger.LogInformation(
                "[mail:log] aucun envoi — to={To} subject={Subject} company={Company}",
                user.Email, $"{company.Name} — réinitialisation de votre mot de passe", company.Id);
        }
        return new ForgotPasswordResult(true, company.Phone, company.Name, MailProvider != "log");
    }
}
