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

/// <summary>`pettyCashCourierExpenseSchema` : la dépense du livreur, sur la journée de son agence.</summary>
public sealed record MyPettyCashExpenseCommand : IRequest<object?>
{
    [JsonIgnore]
    public string? Id { get; init; }

    [JsonIgnore]
    public string? IdempotencyKey { get; init; }

    public string? Label { get; init => field = Zod.Trim(value); }
    public string? Category { get; init => field = Zod.Trim(value); }
    public double? Amount { get; init; }
    public string? Note { get; init => field = Zod.Trim(value); }
    public ProofFileDto? Proof { get; init; }
    public bool? RemoveProof { get; init; }
}

public sealed class MyPettyCashExpenseValidator : ZodValidator<MyPettyCashExpenseCommand>
{
    public MyPettyCashExpenseValidator()
    {
        RuleFor(x => x.Label).Required().MinLength(1, "required").MaxLength(160);
        RuleFor(x => x.Category).Required().MinLength(1, "required").MaxLength(60);
        ProofRules.Amount(this, x => x.Amount);
        RuleFor(x => x.Note).MaxLength(500);
        ProofRules.Proof(this, x => x.Proof);
    }
}

internal sealed class MyPettyCashExpenseHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IFileStorage storage) : IRequestHandler<MyPettyCashExpenseCommand, object?>
{
    public async Task<object?> Handle(MyPettyCashExpenseCommand input, CancellationToken cancellationToken) => input.Id is null
        ? await IdempotentReplay.RunAsync(db, caller, input.IdempotencyKey, "POST /courier/me/petty-cash", () => CreateAsync(input, cancellationToken), cancellationToken)
        : await UpdateAsync(input, cancellationToken);

    private async Task<PettyCashMovementDto> CreateAsync(MyPettyCashExpenseCommand input, CancellationToken cancellationToken)
    {
        var courier = await db.Couriers.AsNoTracking().Where(c => c.Id == caller.CourierId && c.DeletedAt == null).Select(c => new { c.AgencyId, c.CityId }).FirstOrDefaultAsync(cancellationToken)
                      ?? throw BusinessException.NotFound("Fiche livreur introuvable.");
        var agencyId = await db.AgencyForCourierAsync(courier.AgencyId, courier.CityId, cancellationToken)
                       ?? throw BusinessException.Validation("Aucune agence ne peut recevoir cette dépense.", new { reason = "no-agency" });
        var user = await db.Users.AsNoTracking().Where(u => u.Id == caller.UserId).Select(u => new { u.FirstName, u.LastName }).FirstOrDefaultAsync(cancellationToken);
        await CourierExpenses.AssertCategoryAsync(db, input.Category!, null, cancellationToken);
        var day = await PettyCashDays.CourierDayAsync(db, clock, agencyId, cancellationToken);
        var proof = await CourierExpenses.StoreAsync(storage, input.Proof, cancellationToken);
        string id;
        try
        {
            id = await PettyCashDays.WriteAsync(db, clock, [(agencyId, day)], async () =>
            {
                var row = new PettyCashMovement
                {
                    AgencyId = agencyId, Kind = PettyCashKind.EXPENSE, Status = PettyCashStatus.DRAFT,
                    OccurredAt = DateTime.SpecifyKind(PettyCashDays.Parse(day).ToDateTime(new TimeOnly(12, 0)), DateTimeKind.Utc),
                    Amount = Money.FromCents((long)Math.Round(input.Amount!.Value * 100, MidpointRounding.AwayFromZero)), Label = input.Label, Category = input.Category, BuyerId = caller.UserId,
                    BuyerName = user is null ? null : $"{user.FirstName} {user.LastName}", Note = string.IsNullOrEmpty(input.Note) ? null : input.Note, CreatedById = caller.UserId,
                    ProofName = proof is null ? null : input.Proof!.Name, ProofMimeType = proof is null ? null : input.Proof!.MimeType, ProofPath = proof?.Key, CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };
                db.PettyCashMovements.Add(row);
                await db.SaveChangesAsync(cancellationToken);
                return row.Id;
            }, cancellationToken);
        }
        catch
        {
            if (proof is not null) await storage.RemoveAsync(proof.Key, CancellationToken.None);
            throw;
        }
        db.ChangeTracker.Clear();
        return await PettyCashMovements.OneAsync(db, CourierExpenses.Mine(db, caller), id, "Dépense introuvable.", cancellationToken);
    }

    private async Task<PettyCashMovementDto> UpdateAsync(MyPettyCashExpenseCommand input, CancellationToken cancellationToken)
    {
        var current = await CourierExpenses.DraftAsync(db, caller, input.Id!, cancellationToken);
        await CourierExpenses.AssertCategoryAsync(db, input.Category!, current.Category, cancellationToken);
        var proof = await CourierExpenses.StoreAsync(storage, input.Proof, cancellationToken);
        try
        {
            await PettyCashDays.WriteAsync(db, clock, [(current.AgencyId, PettyCashDays.DayOf(current.OccurredAt))], async () =>
            {
                await CourierExpenses.AssertStillDraftAsync(db, current.Id, cancellationToken);
                var row = await db.PettyCashMovements.FirstAsync(m => m.Id == current.Id, cancellationToken);
                row.Label = input.Label;
                row.Category = input.Category;
                row.Amount = Money.FromCents((long)Math.Round(input.Amount!.Value * 100, MidpointRounding.AwayFromZero));
                row.Note = string.IsNullOrEmpty(input.Note) ? null : input.Note;
                row.UpdatedAt = DateTime.UtcNow;
                if (proof is not null) { row.ProofName = input.Proof!.Name; row.ProofMimeType = input.Proof.MimeType; row.ProofPath = proof.Key; }
                else if (input.RemoveProof == true) { row.ProofName = null; row.ProofMimeType = null; row.ProofPath = null; }
                await db.SaveChangesAsync(cancellationToken);
                return true;
            }, cancellationToken, draftOnly: true);
        }
        catch
        {
            if (proof is not null) await storage.RemoveAsync(proof.Key, CancellationToken.None);
            throw;
        }
        db.ChangeTracker.Clear();
        return await PettyCashMovements.OneAsync(db, CourierExpenses.Mine(db, caller), input.Id!, "Dépense introuvable.", cancellationToken);
    }
}
