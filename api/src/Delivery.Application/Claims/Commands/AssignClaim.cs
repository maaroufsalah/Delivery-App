using System.Text.Json.Serialization;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Claims;

/// <summary>`assignClaimSchema` : un compte actif, ou `null` pour la pile commune ; une note interne trace le geste.</summary>
public sealed record AssignClaimCommand : IRequest<ClaimDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public string? AssigneeId
    {
        get;
        init
        {
            field = value;
            AssigneeGiven = true;
        }
    }

    [JsonIgnore]
    public bool AssigneeGiven { get; private init; }
}

public sealed class AssignClaimValidator : ZodValidator<AssignClaimCommand>
{
    public AssignClaimValidator()
    {
        RuleFor(x => x.AssigneeId).Must((x, _) => x.AssigneeGiven).WithMessage(Zod.Expected("string"));
        RuleFor(x => x.AssigneeId).Must(v => v is null || v.Length >= 1).WithMessage(Zod.TooSmallString(1));
    }
}

internal sealed class AssignClaimHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<AssignClaimCommand, ClaimDetailDto>
{
    public async Task<ClaimDetailDto> Handle(AssignClaimCommand input, CancellationToken cancellationToken)
    {
        if (!ClaimRows.CanHandle(caller)) throw BusinessException.Forbidden("Seul le staff assigne une réclamation.");
        var claim = await (await ClaimRows.ScopedAsync(db, db.Claims, caller, cancellationToken)).FirstOrDefaultAsync(c => c.Code == input.Code, cancellationToken)
                    ?? throw BusinessException.NotFound($"Réclamation introuvable : {input.Code}");
        var name = "la pile commune";
        if (!string.IsNullOrEmpty(input.AssigneeId))
        {
            var user = await db.Users.AsNoTracking().Where(u => u.Id == input.AssigneeId && u.IsActive).Select(u => new { u.FirstName, u.LastName }).FirstOrDefaultAsync(cancellationToken)
                       ?? throw BusinessException.Validation("Compte introuvable.", new { field = "assigneeId" });
            name = $"{user.FirstName} {user.LastName}".Trim();
        }
        var author = await ClaimRows.AuthorNameAsync(db, caller.UserId, cancellationToken);
        claim.AssigneeId = input.AssigneeId;
        db.ClaimEvents.Add(new ClaimEvent
        {
            ClaimId = claim.Id, Body = string.IsNullOrEmpty(input.AssigneeId) ? "Rendue à la pile commune." : $"Assignée à {name}.", IsInternal = true, AuthorId = caller.UserId,
            AuthorName = author,
        });
        await db.SaveChangesAsync(cancellationToken);
        return await ClaimRows.DetailAsync(db, caller, clock, input.Code, cancellationToken);
    }
}
