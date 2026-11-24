using System.Text.Json.Serialization;
using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Announcements;

/// <summary>`update` : la saisie complète ; `image` absente garde l'image, `null` la retire, une nouvelle la remplace.</summary>
public sealed record UpdateAnnouncementCommand : AnnouncementFields, IRequest<AnnouncementDetailDto>
{
    [JsonIgnore]
    public string Id { get; init; } = string.Empty;
}

public sealed class UpdateAnnouncementValidator : AnnouncementFieldsValidator<UpdateAnnouncementCommand>;

internal sealed class UpdateAnnouncementHandler(IAppDbContext db, IClock clock, ICurrentUser caller, IFileStorage storage) : IRequestHandler<UpdateAnnouncementCommand, AnnouncementDetailDto>
{
    public async Task<AnnouncementDetailDto> Handle(UpdateAnnouncementCommand input, CancellationToken cancellationToken)
    {
        var row = await db.Announcements.FirstOrDefaultAsync(a => a.Id == input.Id && a.DeletedAt == null, cancellationToken) ?? throw BusinessException.NotFound("Actualité introuvable.");
        var previous = row.ImageKey;
        var image = input.Image is null ? ((string Key, string Filename)?)null : await AnnouncementRows.StoreImageAsync(storage, caller, input.Image, cancellationToken);
        try
        {
            AnnouncementRows.Apply(row, input, clock.UtcNow);
            if (input.ImageGiven)
            {
                row.ImageKey = image?.Key;
                row.ImageName = image?.Filename;
            }
            await db.SaveChangesAsync(cancellationToken);
            if (input.ImageGiven && previous is not null) await storage.RemoveAsync(previous, cancellationToken);
            return AnnouncementRows.ToDetail(row);
        }
        catch
        {
            if (image is not null) await storage.RemoveAsync(image.Value.Key, CancellationToken.None);
            throw;
        }
    }
}
