using System.Text.Json;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.Notifications;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Claims;

/// <summary>`createClaimSchema` : un vendeur, un livreur (sur un colis confié) ou l'agence (pour un vendeur) ouvre une réclamation.</summary>
public sealed record CreateClaimCommand : IRequest<ClaimDetailDto>
{
    public string? TypeId { get; init; }
    public string? Subject { get; init => field = Zod.Trim(value); }
    public string? Message { get; init => field = Zod.Trim(value); }
    public string? ParcelCode { get; init => field = Zod.Trim(value); }
    public string? ClientId { get; init; }
    public IReadOnlyList<ClaimAttachmentInput>? Attachments { get; init; }
}

public sealed class CreateClaimValidator : ZodValidator<CreateClaimCommand>
{
    public CreateClaimValidator()
    {
        RuleFor(x => x.TypeId).Required().MinLength(1, "required");
        RuleFor(x => x.Subject).Required().MinLength(1, "required").MaxLength(160);
        RuleFor(x => x.Message).Required().MinLength(1, "required").MaxLength(4000);
        RuleFor(x => x.ParcelCode).MaxLength(40);
        RuleFor(x => x.Attachments).Custom(ClaimRows.CheckAttachments);
    }
}

internal sealed class CreateClaimHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IFileStorage storage) : IRequestHandler<CreateClaimCommand, ClaimDetailDto>
{
    public async Task<ClaimDetailDto> Handle(CreateClaimCommand input, CancellationToken cancellationToken)
    {
        var clientId = await ResolveClientAsync(input, cancellationToken);
        var type = await db.ClaimTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == input.TypeId && t.DeletedAt == null && t.IsActive, cancellationToken)
                   ?? throw BusinessException.Validation("Type de réclamation inconnu.", new { field = "typeId" });
        var parcelCode = input.ParcelCode ?? string.Empty;
        string? parcelId = null;
        if (type.SubjectType == ClaimSubjectType.PARCEL)
        {
            if (parcelCode.Length == 0) throw BusinessException.Validation("Ce type de réclamation porte sur un colis précis.", new { field = "parcelCode" });
            parcelId = await db.Parcels.AsNoTracking().Where(p => p.Code == parcelCode && p.ClientId == clientId && p.DeletedAt == null).Select(p => p.Id).FirstOrDefaultAsync(cancellationToken)
                       ?? throw BusinessException.Validation($"Colis introuvable chez cet e-commerçant : {parcelCode}", new { field = "parcelCode" });
        }
        var claimAgency = await ClaimRows.AgencyOfAsync(db, parcelId, clientId, cancellationToken);
        if (!caller.IsSuperAdmin() && caller.ClientId is null && caller.CourierId is null
            && (claimAgency is null || !(await ClaimRows.AgenciesAsync(db, caller, cancellationToken)).Contains(claimAgency)))
        {
            throw BusinessException.Forbidden("Cette réclamation relève d’une autre agence.", new { reason = "claim-other-agency" });
        }
        var now = clock.UtcNow;
        var dueAt = await DueDateAsync(now, cancellationToken);
        var author = await ClaimRows.AuthorNameAsync(db, caller.UserId, cancellationToken);
        var files = await ClaimRows.StoreAllAsync(storage, caller, input.Attachments, cancellationToken);
        string code;
        try
        {
            await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
            {
                code = await DocumentCodes.ReserveAsync(db, ClaimRows.Prefix, now, cancellationToken);
                var claim = new Claim
                {
                    Code = code, ClientId = clientId, TypeId = type.Id, Status = ClaimStatus.OPEN, Subject = input.Subject!, Message = input.Message!, ParcelId = parcelId,
                    ParcelCode = parcelId is null ? null : parcelCode, DueAt = dueAt, CreatedById = caller.UserId, AuthorName = author, CreatedAt = now,
                };
                db.Claims.Add(claim);
                db.ClaimEvents.Add(new ClaimEvent { ClaimId = claim.Id, Status = ClaimStatus.OPEN, AuthorId = caller.UserId, AuthorName = author, CreatedAt = now });
                foreach (var file in files)
                {
                    db.ClaimAttachments.Add(new ClaimAttachment
                    {
                        ClaimId = claim.Id, Filename = file.Filename, StorageKey = file.Key, MimeType = file.MimeType, SizeBytes = file.SizeBytes, UploadedById = caller.UserId,
                    });
                }
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            db.ChangeTracker.Clear();
            await ClaimRows.RemoveAllAsync(storage, files);
            throw;
        }
        // Les agents de l'agence de la réclamation (ceux qui peuvent l'ouvrir) et les super-admins.
        await Notifier.NotifyStaffAsync(db, claimAgency ?? caller.AgencyId, "claim-opened", "claims.read", new Dictionary<string, object> { ["code"] = code },
            new Dictionary<string, string> { ["claimCode"] = code }, cancellationToken, withSuperAdmins: true);
        return await ClaimRows.DetailAsync(db, caller, clock, code, cancellationToken);
    }

    /// <summary>`dueDate` : `claims.resolutionDays` (réglage d'agence d'abord) ; zéro, pas d'échéance.</summary>
    private async Task<DateTime?> DueDateAsync(DateTime from, CancellationToken cancellationToken)
    {
        var days = await db.SettingAsync(SettingKeys.ClaimResolutionDays, cancellationToken, caller.AgencyId);
        var value = days is { ValueKind: JsonValueKind.Number } d && double.IsFinite(d.GetDouble()) ? Math.Floor(d.GetDouble()) : 0;
        return value <= 0 ? null : from.AddDays(value);
    }

    /// <summary>`resolveClient` : le vendeur pour lui-même, le livreur par le colis confié, l'agence pour le vendeur choisi.</summary>
    private async Task<string> ResolveClientAsync(CreateClaimCommand input, CancellationToken cancellationToken)
    {
        if (ClaimRows.IsMerchant(caller))
        {
            if (!string.IsNullOrEmpty(input.ClientId) && input.ClientId != caller.ClientId) throw BusinessException.Forbidden("Réclamation impossible pour un autre e-commerçant.");
            return caller.ClientId!;
        }
        if (caller.CourierId is not null)
        {
            var code = input.ParcelCode ?? string.Empty;
            if (code.Length == 0) throw BusinessException.Validation("Indiquez le colis concerné.", new { field = "parcelCode" });
            return await db.Parcels.AsNoTracking().Where(p => p.Code == code && p.CourierId == caller.CourierId && p.DeletedAt == null).Select(p => p.ClientId).FirstOrDefaultAsync(cancellationToken)
                   ?? throw BusinessException.Validation($"Ce colis ne vous est pas confié : {code}", new { field = "parcelCode" });
        }
        if (string.IsNullOrEmpty(input.ClientId)) throw BusinessException.Validation("Choisissez l’e-commerçant concerné.", new { field = "clientId" });
        return await db.Clients.AsNoTracking().Where(c => c.Id == input.ClientId && c.DeletedAt == null).Select(c => c.Id).FirstOrDefaultAsync(cancellationToken)
               ?? throw BusinessException.NotFound("E-commerçant introuvable.");
    }
}
