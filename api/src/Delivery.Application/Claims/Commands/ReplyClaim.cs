using System.Text.Json.Serialization;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Application.Notifications;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Claims;

/// <summary>`replyClaimSchema` : un message, un changement de statut, des pièces jointes, ou tout à la fois.</summary>
public sealed record ReplyClaimCommand : IRequest<ClaimDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public string? Body { get; init => field = Zod.Trim(value); }
    public string? Status { get; init; }
    public bool? IsInternal { get; init; }
    public IReadOnlyList<ClaimAttachmentInput>? Attachments { get; init; }
}

public sealed class ReplyClaimValidator : ZodValidator<ReplyClaimCommand>
{
    public ReplyClaimValidator()
    {
        RuleFor(x => x.Body).MaxLength(4000);
        RuleFor(x => x.Status).OneOf(ClaimRows.Statuses);
        RuleFor(x => x.Attachments).Custom(ClaimRows.CheckAttachments);
        RuleFor(x => x.Body).Must((x, body) => !string.IsNullOrEmpty(body) || x.Status is not null || x.Attachments is { Count: > 0 }).WithMessage("required")
            .When(x => (x.Body is null || x.Body.Length <= 4000) && (x.Status is null || ClaimRows.Statuses.Contains(x.Status)));
    }
}

internal sealed class ReplyClaimHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IFileStorage storage) : IRequestHandler<ReplyClaimCommand, ClaimDetailDto>
{
    public async Task<ClaimDetailDto> Handle(ReplyClaimCommand input, CancellationToken cancellationToken)
    {
        var claim = await ClaimRows.Scoped(db.Claims, caller).FirstOrDefaultAsync(c => c.Code == input.Code, cancellationToken)
                    ?? throw BusinessException.NotFound($"Réclamation introuvable : {input.Code}");
        var handler = ClaimRows.CanHandle(caller);
        var from = ClaimRows.Wire(claim.Status);
        var isInternal = input.IsInternal ?? false;
        if (input.Status is not null && !handler) throw BusinessException.Forbidden("Seul le staff change le statut d’une réclamation.");
        if (isInternal && !handler) throw BusinessException.Forbidden("Une note d’agence n’est pas écrite par l’e-commerçant.");
        if (input.Status is not null && input.Status != from && !ClaimRows.Transitions[from].Contains(input.Status))
        {
            throw BusinessException.Transition($"Transition interdite : {from} → {input.Status}", new { from, to = input.Status });
        }
        var now = clock.UtcNow;
        var author = await ClaimRows.AuthorNameAsync(db, caller.UserId, cancellationToken);
        var files = await ClaimRows.StoreAllAsync(storage, caller, input.Attachments, cancellationToken);
        var next = input.Status is not null && input.Status != from ? input.Status : null;
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var entry = new ClaimEvent
            {
                ClaimId = claim.Id, Status = next is null ? null : ClaimRows.Parse(next), Body = string.IsNullOrEmpty(input.Body) ? null : input.Body, IsInternal = isInternal,
                AuthorId = caller.UserId, AuthorName = author, CreatedAt = now,
            };
            db.ClaimEvents.Add(entry);
            foreach (var file in files)
            {
                db.ClaimAttachments.Add(new ClaimAttachment
                {
                    ClaimId = claim.Id, EventId = entry.Id, Filename = file.Filename, StorageKey = file.Key, MimeType = file.MimeType, SizeBytes = file.SizeBytes, UploadedById = caller.UserId,
                });
            }
            if (next is not null)
            {
                claim.Status = ClaimRows.Parse(next);
                claim.ResolvedAt = ClaimRows.Closed.Contains(next) ? now : null;
            }
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            db.ChangeTracker.Clear();
            await ClaimRows.RemoveAllAsync(storage, files);
            throw;
        }
        if (!ClaimRows.IsMerchant(caller) && !isInternal)
        {
            await Notifier.NotifyMerchantAsync(db, claim.ClientId, "claim-answered", new Dictionary<string, object> { ["code"] = input.Code },
                new Dictionary<string, string> { ["claimCode"] = input.Code }, cancellationToken);
        }
        return await ClaimRows.DetailAsync(db, caller, clock, input.Code, cancellationToken);
    }
}
