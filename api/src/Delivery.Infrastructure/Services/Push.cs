using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Delivery.Application.Notifications;
using Delivery.Domain.Entities;
using Delivery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Delivery.Infrastructure.Services;

/// <summary>
/// Les notifications archivées, en attente de leur push. Le centre de notifications reste la source :
/// le push part après l'écriture, hors de la requête, et ne la ralentit ni ne la fait échouer.
/// </summary>
public sealed class PushQueue
{
    private readonly Channel<string> channel = Channel.CreateBounded<string>(new BoundedChannelOptions(10_000) { FullMode = BoundedChannelFullMode.DropOldest });

    public void Enqueue(string notificationId) => channel.Writer.TryWrite(notificationId);

    public ChannelReader<string> Reader => channel.Reader;
}

/// <summary>Repère chaque `Notification` écrite et la confie à <see cref="PushQueue"/> une fois l'écriture faite.</summary>
public sealed class PushInterceptor(PushQueue queue) : SaveChangesInterceptor
{
    private List<Notification> pending = [];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Flush();
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Flush();
        return base.SavedChanges(eventData, result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => pending = [];

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        pending = [];
        return Task.CompletedTask;
    }

    private void Capture(DbContext? context)
    {
        if (context is null) return;
        pending = context.ChangeTracker.Entries<Notification>().Where(e => e.State == EntityState.Added).Select(e => e.Entity).ToList();
    }

    private void Flush()
    {
        foreach (var notification in pending) queue.Enqueue(notification.Id);
        pending = [];
    }
}

public sealed record PushMessage(string To, string Title, string Body, IReadOnlyDictionary<string, string> Data);

public sealed record PushResult(string Token, bool Ok, string? Error);

/// <summary>`push.service.ts` : l'API d'Expo, par lots de 100, 10 s au plus par lot ; une erreur se journalise, jamais ne remonte.</summary>
public sealed class ExpoPushClient(HttpClient http, ILogger<ExpoPushClient> logger)
{
    public const string Endpoint = "https://exp.host/--/api/v2/push/send";
    private const int Chunk = 100;

    private sealed record Ticket(string? Status, string? Message, TicketDetails? Details);

    private sealed record TicketDetails(string? Error);

    private sealed record Tickets(List<Ticket>? Data);

    private sealed record ExpoMessage(
        [property: JsonPropertyName("to")] string To,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("body")] string Body,
        [property: JsonPropertyName("data")] IReadOnlyDictionary<string, string> Data,
        [property: JsonPropertyName("sound")] string Sound = "default",
        [property: JsonPropertyName("priority")] string Priority = "high",
        [property: JsonPropertyName("channelId")] string ChannelId = "default");

    public async Task<IReadOnlyList<PushResult>> SendAsync(IReadOnlyList<PushMessage> messages, CancellationToken cancellationToken)
    {
        var results = new List<PushResult>();
        foreach (var chunk in messages.Chunk(Chunk)) results.AddRange(await SendChunkAsync(chunk, cancellationToken));
        return results;
    }

    private async Task<IReadOnlyList<PushResult>> SendChunkAsync(PushMessage[] chunk, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            using var response = await http.PostAsJsonAsync(Endpoint, chunk.Select(m => new ExpoMessage(m.To, m.Title, m.Body, m.Data)), timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Expo a refusé le lot : {Status}", (int)response.StatusCode);
                return chunk.Select(m => new PushResult(m.To, false, "http-error")).ToList();
            }
            var payload = await response.Content.ReadFromJsonAsync<Tickets>(new JsonSerializerOptions(JsonSerializerDefaults.Web), timeout.Token);
            var tickets = payload?.Data ?? [];
            return chunk.Select((m, i) =>
            {
                var ticket = i < tickets.Count ? tickets[i] : null;
                return ticket is null || ticket.Status == "ok" ? new PushResult(m.To, true, null) : new PushResult(m.To, false, ticket.Details?.Error ?? "unknown");
            }).ToList();
        }
        catch (Exception error) when (error is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Envoi push impossible : {Message}", error.Message);
            return chunk.Select(m => new PushResult(m.To, false, "unreachable")).ToList();
        }
    }

    /// <summary>`deadTokens` : l'appareil désinscrit chez Expo.</summary>
    public static IReadOnlyList<string> DeadTokens(IEnumerable<PushResult> results) => results.Where(r => r.Error == "DeviceNotRegistered").Select(r => r.Token).ToList();
}

/// <summary>La fin de `dispatch` : chaque appareil du destinataire reçoit le texte dans sa langue ; les jetons morts disparaissent.</summary>
public static class PushDispatch
{
    public static async Task SendAsync(AppDbContext db, ExpoPushClient client, IReadOnlyList<string> notificationIds, CancellationToken cancellationToken)
    {
        var rows = await db.Notifications.AsNoTracking().Where(n => notificationIds.Contains(n.Id)).Select(n => new { n.UserId, n.Topic, n.Params, n.Data }).ToListAsync(cancellationToken);
        if (rows.Count == 0) return;
        var userIds = rows.Select(r => r.UserId).Distinct().ToList();
        var devices = await db.PushTokens.AsNoTracking().Where(t => userIds.Contains(t.UserId)).Select(t => new { t.UserId, t.Token, t.Locale }).ToListAsync(cancellationToken);
        var messages = new List<PushMessage>();
        foreach (var row in rows)
        {
            var parameters = Strings(row.Params);
            var data = new Dictionary<string, string> { ["topic"] = row.Topic };
            foreach (var (key, value) in Strings(row.Data)) data[key] = value;
            foreach (var device in devices.Where(d => d.UserId == row.UserId))
            {
                var copy = NotificationCopy.For(row.Topic, device.Locale, parameters);
                if (copy is not null) messages.Add(new PushMessage(device.Token, copy.Title, copy.Body, data));
            }
        }
        if (messages.Count == 0) return;
        var dead = ExpoPushClient.DeadTokens(await client.SendAsync(messages, cancellationToken));
        if (dead.Count > 0) await db.PushTokens.Where(t => dead.Contains(t.Token)).ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>Les valeurs JSON telles que `String(value)` les écrirait.</summary>
    public static Dictionary<string, string> Strings(string json)
    {
        var values = new Dictionary<string, string>();
        using var document = JsonDocument.Parse(string.IsNullOrEmpty(json) ? "{}" : json);
        if (document.RootElement.ValueKind != JsonValueKind.Object) return values;
        foreach (var property in document.RootElement.EnumerateObject())
        {
            values[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString()!,
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "null",
                _ => property.Value.GetRawText(),
            };
        }
        return values;
    }
}
