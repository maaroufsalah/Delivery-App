using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Idempotency;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.PettyCash;

/// <summary>`pettyCashMovementSchema` : un approvisionnement ou une dépense saisis par l'agence.</summary>
public sealed record PettyCashMovementCommand : IRequest<PettyCashMovementDto>
{
    [JsonIgnore]
    public string? Id { get; init; }

    public string? Kind { get; init; }
    public string? Date { get; init; }
    public double? Amount { get; init; }
    public string? AgencyId { get; init; }
    public string? Label { get; init => field = Zod.Trim(value); }
    public string? Category { get; init => field = Zod.Trim(value); }
    public string? BuyerId { get; init => field = Zod.Trim(value); }
    public string? Note { get; init => field = Zod.Trim(value); }
    public ProofFileDto? Proof { get; init; }
    public bool? RemoveProof { get; init; }
}

public sealed class PettyCashMovementValidator : ZodValidator<PettyCashMovementCommand>
{
    public PettyCashMovementValidator()
    {
        RuleFor(x => x.Kind).Required().OneOf(PettyCashMovements.Kinds);
        RuleFor(x => x.Date).Required().Matches(@"^\d{4}-\d{2}-\d{2}$").WithMessage("invalid-date");
        ProofRules.Amount(this, x => x.Amount);
        RuleFor(x => x.AgencyId).Required().MinLength(1, "required");
        RuleFor(x => x.Label).MaxLength(160);
        RuleFor(x => x.Category).MaxLength(60);
        RuleFor(x => x.BuyerId).MaxLength(40);
        RuleFor(x => x.Note).MaxLength(500);
        ProofRules.Proof(this, x => x.Proof);
    }

    protected override void Refine(PettyCashMovementCommand value, Action<string, string> issue)
    {
        if (value.Kind != "expense") return;
        if (string.IsNullOrEmpty(value.Label)) issue(ValidationPaths.Raw + "label", "required");
        if (string.IsNullOrEmpty(value.Category)) issue(ValidationPaths.Raw + "category", "required");
        if (string.IsNullOrEmpty(value.BuyerId)) issue(ValidationPaths.Raw + "buyerId", "required");
    }
}

internal sealed class PettyCashMovementHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IFileStorage storage) : IRequestHandler<PettyCashMovementCommand, PettyCashMovementDto>
{
    public async Task<PettyCashMovementDto> Handle(PettyCashMovementCommand input, CancellationToken cancellationToken)
    {
        var scoped = PettyCashMovements.Scoped(db.PettyCashMovements.AsNoTracking(), caller);
        var current = input.Id is null ? null
            : await scoped.Where(m => m.Id == input.Id).Select(m => new { m.Id, m.AgencyId, m.OccurredAt, m.Category, m.BuyerId }).FirstOrDefaultAsync(cancellationToken)
              ?? throw BusinessException.NotFound("Mouvement de petite caisse introuvable.");
        await PettyCashMovements.AssertAgencyReachableAsync(db, caller, input.AgencyId!, cancellationToken);
        var expense = input.Kind == "expense";
        string? buyerName = null;
        if (expense)
        {
            var categories = await PettyCashMovements.CategoriesAsync(db, cancellationToken);
            if (!categories.Contains(input.Category ?? string.Empty) && current?.Category != input.Category) throw BusinessException.Validation("Catégorie inconnue.", new { field = "category" });
            var keepsBuyer = current?.BuyerId is not null && current.BuyerId == input.BuyerId;
            var buyers = keepsBuyer ? db.Users.AsNoTracking() : PettyCashMovements.Buyers(db, caller);
            var buyerId = (keepsBuyer ? current!.BuyerId : input.BuyerId) ?? Scope.DenyAllId;
            var buyer = await buyers.Where(u => u.Id == buyerId).Select(u => new { u.FirstName, u.LastName }).FirstOrDefaultAsync(cancellationToken)
                        ?? throw BusinessException.Validation("Ce membre de l’équipe est introuvable.", new { field = "buyerId" });
            buyerName = $"{buyer.FirstName} {buyer.LastName}";
        }
        var occurredAt = DateTime.SpecifyKind(DateTime.ParseExact($"{input.Date}T12:00:00", "yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Utc);
        var amount = Money.FromCents((long)Math.Round(input.Amount!.Value * 100, MidpointRounding.AwayFromZero));
        var targets = new List<(string, string)> { (input.AgencyId!, input.Date!) };
        if (current is not null) targets.Insert(0, (current.AgencyId, PettyCashDays.DayOf(current.OccurredAt)));
        var id = await PettyCashDays.WriteAsync(db, clock, targets, async () =>
        {
            var row = current is null ? new PettyCashMovement { CreatedById = caller.UserId, CreatedAt = DateTime.UtcNow } : await db.PettyCashMovements.FirstAsync(m => m.Id == current.Id, cancellationToken);
            row.Kind = expense ? PettyCashKind.EXPENSE : PettyCashKind.SUPPLY;
            row.OccurredAt = occurredAt;
            row.Amount = amount;
            row.AgencyId = input.AgencyId!;
            row.Label = expense ? ReturnLabel(input.Label) : null;
            row.Category = expense ? ReturnLabel(input.Category) : null;
            row.BuyerId = expense ? input.BuyerId : null;
            row.BuyerName = buyerName;
            row.Note = string.IsNullOrEmpty(input.Note) ? null : input.Note;
            row.UpdatedAt = DateTime.UtcNow;
            if (current is not null && input.RemoveProof == true && input.Proof is null) { row.ProofName = null; row.ProofMimeType = null; row.ProofPath = null; }
            if (current is null) db.PettyCashMovements.Add(row);
            await db.SaveChangesAsync(cancellationToken);
            return row.Id;
        }, cancellationToken);
        db.ChangeTracker.Clear();
        if (input.Proof is not null) await PettyCashMovements.AttachProofAsync(db, storage, id, input.Proof, $"petty-cash/{id}", cancellationToken);
        return await PettyCashMovements.OneAsync(db, PettyCashMovements.Scoped(db.PettyCashMovements.AsNoTracking(), caller), id, "Mouvement de petite caisse introuvable.", cancellationToken);
    }

    private static string? ReturnLabel(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
