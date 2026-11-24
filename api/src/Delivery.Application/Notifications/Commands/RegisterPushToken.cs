using System.Text.RegularExpressions;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Persistence;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Notifications;

/// <summary>`pushTokenSchema` : l'appareil s'enregistre (ou se réenregistre) pour les pushes.</summary>
public sealed partial record RegisterPushTokenCommand : IRequest<PushTokenDto>
{
    public string? Token { get; init => field = Zod.Trim(value); }
    public string? Platform { get; init; }
    public string? Locale { get; init; }
    public string? AppVersion { get; init => field = Zod.Trim(value); }

    [GeneratedRegex(@"^Expo(nent)?PushToken\[[A-Za-z0-9._-]+\]$")]
    public static partial Regex ExpoToken();
}

public sealed class RegisterPushTokenValidator : ZodValidator<RegisterPushTokenCommand>
{
    public RegisterPushTokenValidator()
    {
        RuleFor(x => x.Token).Required().Pattern(RegisterPushTokenCommand.ExpoToken(), "invalid-push-token");
        RuleFor(x => x.Platform).Required().OneOf(["ios", "android"]);
        RuleFor(x => x.Locale).OneOf(["fr", "ar", "en"]);
        RuleFor(x => x.AppVersion).MaxLength(32);
    }
}

internal sealed class RegisterPushTokenHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<RegisterPushTokenCommand, PushTokenDto>
{
    public async Task<PushTokenDto> Handle(RegisterPushTokenCommand input, CancellationToken cancellationToken)
    {
        var token = input.Token!;
        var platform = input.Platform == "ios" ? DevicePlatform.IOS : DevicePlatform.ANDROID;
        for (var attempt = 0; ; attempt++)
        {
            var row = await db.PushTokens.FirstOrDefaultAsync(t => t.Token == token, cancellationToken);
            if (row is null)
            {
                db.PushTokens.Add(new PushToken { UserId = caller.UserId, Token = token, Platform = platform, Locale = input.Locale ?? "fr", AppVersion = input.AppVersion, LastSeenAt = clock.UtcNow });
            }
            else
            {
                row.UserId = caller.UserId;
                row.Platform = platform;
                row.Locale = input.Locale ?? "fr";
                row.AppVersion = input.AppVersion;
                row.LastSeenAt = clock.UtcNow;
            }
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return new PushTokenDto(token);
            }
            catch (DbUpdateException error) when (attempt == 0 && DbErrors.IsUniqueViolation(error))
            {
                db.ChangeTracker.Clear();
            }
        }
    }
}
