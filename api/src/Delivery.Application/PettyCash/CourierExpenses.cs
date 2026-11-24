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

/// <summary>`PettyCashCourierService` : les dépenses du livreur, brouillons jusqu'à la décision de l'agence.</summary>
internal static class CourierExpenses
{
    public static IQueryable<PettyCashMovement> Mine(IAppDbContext db, ICurrentUser caller)
    {
        if (caller.CourierId is null || caller.UserId is null) return db.PettyCashMovements.AsNoTracking().Where(m => m.Id == Scope.DenyAllId);
        var userId = caller.UserId;
        return db.PettyCashMovements.AsNoTracking().Where(m => m.Kind == PettyCashKind.EXPENSE && m.CreatedById == userId && m.BuyerId == userId);
    }

    public static BusinessException NotDraft() => BusinessException.Forbidden("Cette dépense a déjà été traitée : elle ne se modifie plus.", new { reason = "petty-cash-not-draft" });

    public static async Task<(string Id, string AgencyId, DateTime OccurredAt, string? Category)> DraftAsync(IAppDbContext db, ICurrentUser caller, string id, CancellationToken cancellationToken)
    {
        var row = await Mine(db, caller).Where(m => m.Id == id).Select(m => new { m.Id, m.AgencyId, m.OccurredAt, m.Category, m.Status }).FirstOrDefaultAsync(cancellationToken)
                  ?? throw BusinessException.NotFound("Dépense introuvable.");
        if (row.Status != PettyCashStatus.DRAFT) throw NotDraft();
        return (row.Id, row.AgencyId, row.OccurredAt, row.Category);
    }

    public static async Task AssertStillDraftAsync(IAppDbContext db, string id, CancellationToken cancellationToken)
    {
        if (!await db.PettyCashMovements.AnyAsync(m => m.Id == id && m.Status == PettyCashStatus.DRAFT, cancellationToken)) throw NotDraft();
    }

    public static async Task AssertCategoryAsync(IAppDbContext db, string category, string? current, CancellationToken cancellationToken)
    {
        if (!(await PettyCashMovements.CategoriesAsync(db, cancellationToken)).Contains(category) && current != category) throw BusinessException.Validation("Catégorie inconnue.", new { field = "category" });
    }

    public static async Task<StoredFile?> StoreAsync(IFileStorage storage, ProofFileDto? proof, CancellationToken cancellationToken) =>
        proof is null ? null : await storage.PutAsync("petty-cash/courier", Convert.FromBase64String(proof.Content!), proof.MimeType!, cancellationToken);
}
