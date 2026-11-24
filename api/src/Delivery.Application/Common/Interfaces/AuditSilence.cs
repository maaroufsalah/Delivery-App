namespace Delivery.Application.Common.Interfaces;

/// <summary>`runWithoutAudit` : les écritures faites dans ce bloc ne vont pas au journal.</summary>
public static class AuditSilence
{
    private static readonly AsyncLocal<bool> Silenced = new();

    public static bool IsSilenced => Silenced.Value;

    public static async Task<T> RunAsync<T>(Func<Task<T>> action)
    {
        var previous = Silenced.Value;
        Silenced.Value = true;
        try
        {
            return await action();
        }
        finally
        {
            Silenced.Value = previous;
        }
    }

    public static async Task RunAsync(Func<Task> action) => await RunAsync(async () =>
    {
        await action();
        return true;
    });
}
