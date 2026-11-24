using System.Text.Json;

namespace Delivery.Application.Notifications;

public sealed record PushTokenDto(string Token);

public sealed record RemovedDto(int Removed);

public sealed record UnreadDto(int Unread);

public sealed record NotificationTopicSettingDto(string Key, bool Enabled, bool Critical);

public sealed record NotificationSettingsDto(string? Audience, IReadOnlyList<NotificationTopicSettingDto> Topics);

public sealed record NotificationItemDto(string Id, string Topic, JsonElement Params, JsonElement Data, DateTime? ReadAt, DateTime CreatedAt);

public sealed record NotificationFeedDto(IReadOnlyList<NotificationItemDto> Items, int Unread);
