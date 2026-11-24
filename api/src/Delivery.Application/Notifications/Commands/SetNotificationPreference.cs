using Delivery.Domain.Common;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Persistence;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Notifications;

/// <summary>`notificationPreferenceSchema` : coupe ou rouvre un sujet de son audience ; un sujet critique ne se coupe pas.</summary>
public sealed record SetNotificationPreferenceCommand : IRequest<NotificationSettingsDto>
{
    public string? Topic { get; init => field = Zod.Trim(value); }
    public bool? Enabled { get; init; }
}

public sealed class SetNotificationPreferenceValidator : ZodValidator<SetNotificationPreferenceCommand>
{
    public SetNotificationPreferenceValidator()
    {
        RuleFor(x => x.Topic).Required().Must(t => t is null || Notifier.Find(t) is not null).WithMessage("unknown-topic");
        RuleFor(x => x.Enabled).Required("boolean");
    }
}

internal sealed class SetNotificationPreferenceHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<SetNotificationPreferenceCommand, NotificationSettingsDto>
{
    public async Task<NotificationSettingsDto> Handle(SetNotificationPreferenceCommand input, CancellationToken cancellationToken)
    {
        var topic = input.Topic!;
        var definition = Notifier.Find(topic) ?? throw BusinessException.Validation($"Sujet inconnu : {topic}", new { reason = "unknown-topic" });
        if (definition.Audience != NotificationCenter.AudienceOf(caller)) throw BusinessException.Forbidden("Ce sujet ne vous concerne pas.", new { reason = "topic-not-yours" });
        if (definition.Critical) throw BusinessException.Validation("Ce sujet ne peut pas être désactivé.", new { reason = "topic-critical" });
        for (var attempt = 0; ; attempt++)
        {
            var row = await db.NotificationPreferences.FirstOrDefaultAsync(p => p.UserId == caller.UserId && p.Topic == topic, cancellationToken);
            if (row is null) db.NotificationPreferences.Add(new NotificationPreference { UserId = caller.UserId, Topic = topic, Enabled = input.Enabled!.Value });
            else row.Enabled = input.Enabled!.Value;
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                break;
            }
            catch (DbUpdateException error) when (attempt == 0 && DbErrors.IsUniqueViolation(error))
            {
                db.ChangeTracker.Clear();
            }
        }
        return await NotificationCenter.SettingsAsync(db, caller, cancellationToken);
    }
}
