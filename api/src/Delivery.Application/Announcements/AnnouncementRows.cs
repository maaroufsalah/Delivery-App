using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Announcements;

public sealed record AnnouncementImageInput
{
    public string? Filename { get; init => field = Zod.Trim(value); }
    public string? MimeType { get; init; }
    public string? Content { get; init; }
}

/// <summary>`announcementSchema` : la même saisie pour publier et pour modifier.</summary>
public abstract record AnnouncementFields
{
    public string? Title { get; init => field = Zod.Trim(value); }
    public string? Intro { get; init => field = Zod.Trim(value); }
    public string? Body { get; init => field = Zod.Trim(value); }
    public string? Audience { get; init; }
    public bool? IsPublished { get; init; }
    public string? PublishedAt { get; init; }

    /// <summary>`null` retire l'image en place ; absent la conserve.</summary>
    public AnnouncementImageInput? Image
    {
        get;
        init
        {
            field = value;
            ImageGiven = true;
        }
    }

    [JsonIgnore]
    public bool ImageGiven { get; private init; }
}

public abstract class AnnouncementFieldsValidator<T> : ZodValidator<T> where T : AnnouncementFields
{
    protected AnnouncementFieldsValidator()
    {
        RuleFor(x => x.Title).Required().MinLength(3, "too-short").MaxLength(160);
        RuleFor(x => x.Intro).MaxLength(300);
        RuleFor(x => x.Body).Required().MinLength(10, "too-short").MaxLength(20_000);
        RuleFor(x => x.Audience).OneOf(AnnouncementRows.Audiences);
        RuleFor(x => x.Image!.Filename).Required().MinLength(1, "required").MaxLength(200).OverridePropertyName("image.filename").When(x => x.Image is not null);
        RuleFor(x => x.Image!.MimeType).Must(v => v is not null && AnnouncementRows.MimeTypes.Contains(v)).WithMessage("unsupported-type").OverridePropertyName("image.mimeType")
            .When(x => x.Image is not null);
        RuleFor(x => x.Image!.Content).Required().MinLength(1, "required").Must(v => v is null || v.Length <= 6_000_000).WithMessage("file-too-large")
            .OverridePropertyName("image.content").When(x => x.Image is not null);
    }
}

/// <summary>`announcements.service.ts` : ce que les lectures et les écritures partagent.</summary>
public static class AnnouncementRows
{
    public static readonly string[] Audiences = ["all", "merchants", "couriers"];
    public static readonly string[] MimeTypes = ["image/jpeg", "image/png", "image/webp"];

    /// <summary>`canWrite` : le personnel et le super-admin lisent aussi les brouillons.</summary>
    public static bool CanWrite(ICurrentUser caller) => caller.Roles.Contains(RoleName.SUPER_ADMIN) || caller.Roles.Contains(RoleName.STAFF);

    /// <summary>`audiencesFor` : un vendeur voit « tous » et « vendeurs », un livreur « tous » et « livreurs ».</summary>
    public static IQueryable<Announcement> ForAudience(IQueryable<Announcement> rows, ICurrentUser caller)
    {
        if (caller.ClientId is not null) return rows.Where(a => a.Audience == AnnouncementAudience.ALL || a.Audience == AnnouncementAudience.MERCHANTS);
        if (caller.CourierId is not null) return rows.Where(a => a.Audience == AnnouncementAudience.ALL || a.Audience == AnnouncementAudience.COURIERS);
        return rows;
    }

    /// <summary>`readableBy` : hors personnel, seulement son audience et une date de publication passée.</summary>
    public static IQueryable<Announcement> Readable(IAppDbContext db, ICurrentUser caller, DateTime now)
    {
        var rows = db.Announcements.AsNoTracking().Where(a => a.DeletedAt == null);
        if (CanWrite(caller)) return rows;
        return ForAudience(rows, caller).Where(a => a.PublishedAt != null && a.PublishedAt <= now);
    }

    public static readonly Expression<Func<Announcement, AnnouncementDetailDto>> Detail = a => new AnnouncementDetailDto
    {
        Id = a.Id, Title = a.Title, Intro = a.Intro, Audience = a.Audience == AnnouncementAudience.ALL ? "all" : a.Audience == AnnouncementAudience.MERCHANTS ? "merchants" : "couriers",
        IsPublished = a.IsPublished, PublishedAt = a.PublishedAt, HasImage = a.ImageKey != null, ImageName = a.ImageName, AuthorName = a.AuthorName, CreatedAt = a.CreatedAt,
        UpdatedAt = a.UpdatedAt, Body = a.Body,
    };

    public static readonly Expression<Func<Announcement, AnnouncementListItemDto>> Item = a => new AnnouncementListItemDto
    {
        Id = a.Id, Title = a.Title, Intro = a.Intro, Audience = a.Audience == AnnouncementAudience.ALL ? "all" : a.Audience == AnnouncementAudience.MERCHANTS ? "merchants" : "couriers",
        IsPublished = a.IsPublished, PublishedAt = a.PublishedAt, HasImage = a.ImageKey != null, ImageName = a.ImageName, AuthorName = a.AuthorName, CreatedAt = a.CreatedAt,
        UpdatedAt = a.UpdatedAt,
    };

    private static readonly Func<Announcement, AnnouncementDetailDto> DetailOf = Detail.Compile();

    public static AnnouncementDetailDto ToDetail(Announcement a) => DetailOf(a);

    /// <summary>`data` : la date saisie, sinon l'instant de publication ; un brouillon sans date garde la sienne.</summary>
    public static void Apply(Announcement row, AnnouncementFields input, DateTime now)
    {
        DateTime? publishedAt = null;
        if (!string.IsNullOrEmpty(input.PublishedAt))
        {
            publishedAt = DateTime.TryParse(input.PublishedAt, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed)
                ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
                : throw BusinessException.Validation("Saisie invalide.", new { issues = new[] { new { path = "publishedAt", message = "invalid-date" } } });
        }
        else if (input.IsPublished == true)
        {
            publishedAt = now;
        }
        row.Title = input.Title!;
        row.Intro = string.IsNullOrEmpty(input.Intro) ? null : input.Intro;
        row.Body = input.Body!;
        row.Audience = Enum.Parse<AnnouncementAudience>(Money.FromWire(input.Audience ?? "all"));
        row.IsPublished = input.IsPublished ?? false;
        if (publishedAt is not null) row.PublishedAt = publishedAt;
    }

    /// <summary>`storeImage` : 4 Mo au plus, nettoyée (1600 px, format conservé).</summary>
    public static async Task<(string Key, string Filename)> StoreImageAsync(IFileStorage storage, ICurrentUser caller, AnnouncementImageInput image, CancellationToken cancellationToken)
    {
        byte[] content;
        try
        {
            content = Convert.FromBase64String(image.Content!);
        }
        catch (FormatException)
        {
            content = [];
        }
        if (content.Length > 4 * 1024 * 1024) throw BusinessException.Validation("L’image dépasse 4 Mo.", new { field = "image" });
        var safe = storage.SanitizeImage(content, image.MimeType!, 1600, preserveFormat: true);
        var stored = await storage.PutAsync($"{caller.CompanyId}/announcements", safe.Content, safe.MimeType, cancellationToken);
        return (stored.Key, image.Filename!);
    }

    public static async Task<string> AuthorNameAsync(IAppDbContext db, string userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => new { u.FirstName, u.LastName }).FirstOrDefaultAsync(cancellationToken);
        return user is null ? "Système" : $"{user.FirstName} {user.LastName}".Trim();
    }
}
