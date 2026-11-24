using System.Linq.Expressions;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Claims;

public sealed record ClaimAttachmentInput
{
    public string? Filename { get; init => field = Zod.Trim(value); }
    public string? MimeType { get; init; }
    public string? Content { get; init; }
}

/// <summary>`claimListQuerySchema`, une fois lu.</summary>
public sealed record ClaimListFilter(string? Search, IReadOnlyList<string>? Status, IReadOnlyList<string>? TypeId, IReadOnlyList<string>? ClientId, string? AssigneeId, string? Late,
    string? Page, string? PageSize);

/// <summary>`claims.service.ts` et `claims.scope.ts` : la portée, le filtre, la projection et les pièces jointes.</summary>
public static class ClaimRows
{
    public const string Prefix = "RCL";
    public static readonly string[] Statuses = ["open", "in-progress", "resolved", "rejected"];
    public static readonly string[] Closed = ["resolved", "rejected"];
    public static readonly string[] AttachmentTypes = ["image/jpeg", "image/png", "image/webp", "application/pdf"];
    private const int MaxAttachmentBytes = 4 * 1024 * 1024;

    public static readonly IReadOnlyDictionary<string, string[]> Transitions = new Dictionary<string, string[]>
    {
        ["open"] = ["in-progress", "resolved", "rejected"], ["in-progress"] = ["resolved", "rejected"], ["resolved"] = ["in-progress"], ["rejected"] = ["in-progress"],
    };

    public static string Wire(ClaimStatus status) => Money.ToWire(status);

    public static ClaimStatus Parse(string wire) => Enum.Parse<ClaimStatus>(Money.FromWire(wire));

    /// <summary>`claimWhere` : le vendeur ses réclamations, le livreur celles de ses colis, le personnel toutes.</summary>
    public static IQueryable<Claim> Scoped(IQueryable<Claim> claims, ICurrentUser caller)
    {
        if (caller.IsSuperAdmin()) return claims;
        if (caller.ClientId is not null) return claims.Where(c => c.ClientId == caller.ClientId);
        if (caller.CourierId is not null) return claims.Where(c => c.Parcel != null && c.Parcel.CourierId == caller.CourierId);
        return claims;
    }

    public static bool IsMerchant(ICurrentUser caller) => !caller.IsSuperAdmin() && caller.ClientId is not null;

    /// <summary>`canHandle` : le personnel traite (statut, note interne, assignation) ; jamais un vendeur.</summary>
    public static bool CanHandle(ICurrentUser caller) => !IsMerchant(caller) && (caller.IsSuperAdmin() || caller.HasRole(RoleName.STAFF));

    public static readonly Expression<Func<Claim, ClaimRow>> Row = c => new ClaimRow
    {
        Id = c.Id, Code = c.Code, Status = c.Status, Subject = c.Subject, TypeId = c.TypeId, TypeLabel = c.Type.Label, TypeCode = c.Type.Code, ClientId = c.ClientId,
        ClientName = c.Client.Name, ParcelCode = c.ParcelCode, AssigneeId = c.AssigneeId,
        AssigneeName = c.Assignee == null ? null : (c.Assignee.FirstName + " " + c.Assignee.LastName).Trim(), AuthorName = c.AuthorName, DueAt = c.DueAt, ResolvedAt = c.ResolvedAt,
        CreatedAt = c.CreatedAt, UpdatedAt = c.UpdatedAt, Message = c.Message,
    };

    public static ClaimListItemDto Item(ClaimRow row, DateTime now)
    {
        var status = Wire(row.Status);
        return new ClaimListItemDto
        {
            Id = row.Id, Code = row.Code, Status = status, Subject = row.Subject, TypeId = row.TypeId, TypeLabel = row.TypeLabel, TypeCode = row.TypeCode, ClientId = row.ClientId,
            ClientName = row.ClientName, ParcelCode = row.ParcelCode, AssigneeId = row.AssigneeId, AssigneeName = row.AssigneeName, AuthorName = row.AuthorName, DueAt = row.DueAt,
            IsLate = row.DueAt is { } due && due < now && !Closed.Contains(status), ResolvedAt = row.ResolvedAt, CreatedAt = row.CreatedAt, UpdatedAt = row.UpdatedAt,
        };
    }

    /// <summary>`where` : multi-filtres, retard, recherche ; un vendeur ne filtre pas par client.</summary>
    public static (IQueryable<Claim> Rows, int Page, int PageSize) Filter(IAppDbContext db, ICurrentUser caller, ClaimListFilter filter, DateTime now)
    {
        var issues = new List<object>();
        var page = ListQueryParams.Coerce(filter.Page, 1, 1, null, "page", issues);
        var pageSize = ListQueryParams.Coerce(filter.PageSize, 20, 5, 200, "pageSize", issues);
        Multi(filter.Status, "status", Statuses, issues);
        Multi(filter.TypeId, "typeId", null, issues);
        Multi(filter.ClientId, "clientId", null, issues);
        if (issues.Count > 0) throw BusinessException.Validation("Saisie invalide.", new { issues });

        var rows = Scoped(db.Claims.AsNoTracking(), caller);
        if (filter.Status is { } statuses)
        {
            var wanted = statuses.Select(Parse).ToList();
            rows = rows.Where(c => wanted.Contains(c.Status));
        }
        if (filter.TypeId is { } types) rows = rows.Where(c => types.Contains(c.TypeId));
        if (filter.ClientId is { } clients && !IsMerchant(caller)) rows = rows.Where(c => clients.Contains(c.ClientId));
        if (!string.IsNullOrEmpty(filter.AssigneeId)) rows = rows.Where(c => c.AssigneeId == filter.AssigneeId);
        // `z.coerce.boolean()` : toute chaîne non vide vaut vrai, « false » compris.
        if (!string.IsNullOrEmpty(filter.Late))
        {
            rows = rows.Where(c => (c.Status == ClaimStatus.OPEN || c.Status == ClaimStatus.IN_PROGRESS) && c.DueAt != null && c.DueAt < now);
        }
        var needle = filter.Search?.Trim();
        if (!string.IsNullOrEmpty(needle))
        {
            var pattern = Like.Contains(needle);
            rows = rows.Where(c => EF.Functions.ILike(c.Code, pattern) || EF.Functions.ILike(c.Subject, pattern) || EF.Functions.ILike(c.Message, pattern)
                                   || (c.ParcelCode != null && EF.Functions.ILike(c.ParcelCode, pattern)) || EF.Functions.ILike(c.Client.Name, pattern));
        }
        return (rows, page, pageSize);
    }

    private static void Multi(IReadOnlyList<string>? values, string path, IReadOnlyCollection<string>? allowed, List<object> issues)
    {
        if (values is null) return;
        if (values.Count > 200) issues.Add(new { path, message = "Too big: expected array to have <=200 items" });
        if (allowed is null) return;
        for (var i = 0; i < values.Count; i++)
        {
            if (!allowed.Contains(values[i])) issues.Add(new { path = $"{path}.{i}", message = Zod.InvalidOption(allowed) });
        }
    }

    public static async Task<ClaimDetailDto> DetailAsync(IAppDbContext db, ICurrentUser caller, IClock clock, string code, CancellationToken cancellationToken)
    {
        var row = await Scoped(db.Claims.AsNoTracking(), caller).Where(c => c.Code == code).Select(Row).FirstOrDefaultAsync(cancellationToken)
                  ?? throw BusinessException.NotFound($"Réclamation introuvable : {code}");
        var handler = CanHandle(caller);
        var attachments = await db.ClaimAttachments.AsNoTracking().Where(a => a.ClaimId == row.Id).OrderBy(a => a.CreatedAt)
            .Select(a => new { a.EventId, File = new ClaimAttachmentDto(a.Id, a.Filename, a.MimeType, a.SizeBytes, a.CreatedAt) }).ToListAsync(cancellationToken);
        var events = await db.ClaimEvents.AsNoTracking().Where(e => e.ClaimId == row.Id && (handler || !e.IsInternal)).OrderBy(e => e.CreatedAt)
            .Select(e => new { e.Id, e.Status, e.Body, e.IsInternal, e.AuthorName, e.CreatedAt }).ToListAsync(cancellationToken);
        var item = Item(row, clock.UtcNow);
        return new ClaimDetailDto
        {
            Id = item.Id, Code = item.Code, Status = item.Status, Subject = item.Subject, TypeId = item.TypeId, TypeLabel = item.TypeLabel, TypeCode = item.TypeCode,
            ClientId = item.ClientId, ClientName = item.ClientName, ParcelCode = item.ParcelCode, AssigneeId = item.AssigneeId, AssigneeName = item.AssigneeName,
            AuthorName = item.AuthorName, DueAt = item.DueAt, IsLate = item.IsLate, ResolvedAt = item.ResolvedAt, CreatedAt = item.CreatedAt, UpdatedAt = item.UpdatedAt,
            Message = row.Message,
            Attachments = attachments.Where(a => a.EventId == null).Select(a => a.File).ToList(),
            Events = events.Select(e => new ClaimEventDto(e.Id, e.Status is { } s ? Wire(s) : null, e.Body, e.IsInternal, e.AuthorName,
                attachments.Where(a => a.EventId == e.Id).Select(a => a.File).ToList(), e.CreatedAt)).ToList(),
            AllowedTransitions = handler ? Transitions[item.Status] : [],
        };
    }

    public sealed record StoredAttachment(string Filename, string Key, string MimeType, int SizeBytes);

    /// <summary>`storeAll` : types admis, 4 Mo au plus par fichier ; stockés tels quels.</summary>
    public static async Task<IReadOnlyList<StoredAttachment>> StoreAllAsync(IFileStorage storage, ICurrentUser caller, IReadOnlyList<ClaimAttachmentInput>? files, CancellationToken cancellationToken)
    {
        var stored = new List<StoredAttachment>();
        foreach (var file in files ?? [])
        {
            if (!AttachmentTypes.Contains(file.MimeType)) throw BusinessException.Validation($"Type de fichier refusé : {file.MimeType}", new { field = "attachments" });
            byte[] content;
            try
            {
                content = Convert.FromBase64String(file.Content!);
            }
            catch (FormatException)
            {
                content = [];
            }
            if (content.Length > MaxAttachmentBytes) throw BusinessException.Validation($"« {file.Filename} » dépasse 4 Mo.", new { field = "attachments" });
            var result = await storage.PutAsync($"{caller.CompanyId}/claims", content, file.MimeType!, cancellationToken);
            stored.Add(new StoredAttachment(file.Filename!, result.Key, result.MimeType, (int)result.SizeBytes));
        }
        return stored;
    }

    public static async Task RemoveAllAsync(IFileStorage storage, IEnumerable<StoredAttachment> files)
    {
        foreach (var file in files) await storage.RemoveAsync(file.Key, CancellationToken.None);
    }

    public static async Task<string> AuthorNameAsync(IAppDbContext db, string userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => new { u.FirstName, u.LastName }).FirstOrDefaultAsync(cancellationToken);
        return user is null ? "Système" : $"{user.FirstName} {user.LastName}".Trim();
    }

    /// <summary>`attachments` : cinq fichiers au plus, chacun validé comme `claimAttachmentSchema`.</summary>
    public static void CheckAttachments<T>(IReadOnlyList<ClaimAttachmentInput>? list, ValidationContext<T> context)
    {
        {
            if (list is null) return;
            if (list.Count > 5) context.AddFailure(ValidationPaths.Raw + "attachments", "too-many-attachments");
            for (var i = 0; i < list.Count; i++)
            {
                var file = list[i];
                var at = $"attachments.{i}.";
                if (file.Filename is null) context.AddFailure(ValidationPaths.Raw + at + "filename", Zod.Expected("string"));
                else if (file.Filename.Length < 1) context.AddFailure(ValidationPaths.Raw + at + "filename", "required");
                else if (file.Filename.Length > 200) context.AddFailure(ValidationPaths.Raw + at + "filename", Zod.TooBigString(200));
                if (file.MimeType is null || !AttachmentTypes.Contains(file.MimeType)) context.AddFailure(ValidationPaths.Raw + at + "mimeType", "unsupported-type");
                if (file.Content is null) context.AddFailure(ValidationPaths.Raw + at + "content", Zod.Expected("string"));
                else if (file.Content.Length < 1) context.AddFailure(ValidationPaths.Raw + at + "content", "required");
                else if (file.Content.Length > 6_000_000) context.AddFailure(ValidationPaths.Raw + at + "content", "file-too-large");
            }
        }
    }
}

/// <summary>Une réclamation lue en base, avant le calcul du retard.</summary>
public sealed record ClaimRow
{
    public string Id { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public ClaimStatus Status { get; init; }
    public string Subject { get; init; } = string.Empty;
    public string TypeId { get; init; } = string.Empty;
    public string TypeLabel { get; init; } = string.Empty;
    public string TypeCode { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string ClientName { get; init; } = string.Empty;
    public string? ParcelCode { get; init; }
    public string? AssigneeId { get; init; }
    public string? AssigneeName { get; init; }
    public string AuthorName { get; init; } = string.Empty;
    public DateTime? DueAt { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public string Message { get; init; } = string.Empty;
}
