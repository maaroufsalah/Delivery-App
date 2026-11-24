using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Entities;
using MediatR;

namespace Delivery.Application.Announcements;

/// <summary>`create` : publiée ou en brouillon ; l'image stockée est retirée si l'écriture échoue.</summary>
public sealed record CreateAnnouncementCommand : AnnouncementFields, IRequest<AnnouncementDetailDto>;

public sealed class CreateAnnouncementValidator : AnnouncementFieldsValidator<CreateAnnouncementCommand>;

internal sealed class CreateAnnouncementHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IFileStorage storage) : IRequestHandler<CreateAnnouncementCommand, AnnouncementDetailDto>
{
    public async Task<AnnouncementDetailDto> Handle(CreateAnnouncementCommand input, CancellationToken cancellationToken)
    {
        var row = new Announcement { CreatedById = caller.UserId };
        AnnouncementRows.Apply(row, input, clock.UtcNow);
        var image = input.Image is null ? ((string Key, string Filename)?)null : await AnnouncementRows.StoreImageAsync(storage, caller, input.Image, cancellationToken);
        try
        {
            row.ImageKey = image?.Key;
            row.ImageName = image?.Filename;
            row.AuthorName = await AnnouncementRows.AuthorNameAsync(db, caller.UserId, cancellationToken);
            db.Announcements.Add(row);
            await db.SaveChangesAsync(cancellationToken);
            return AnnouncementRows.ToDetail(row);
        }
        catch
        {
            if (image is not null) await storage.RemoveAsync(image.Value.Key, CancellationToken.None);
            throw;
        }
    }
}
