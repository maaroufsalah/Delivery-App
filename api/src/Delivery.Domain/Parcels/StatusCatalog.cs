namespace Delivery.Domain.Parcels;

/// <summary>Un statut logistique (`PARCEL_STATUSES`).</summary>
public sealed record StatusDefinition(int Id, string Key, string Group, string Color, string Icon);

public static partial class StatusCatalog
{
    // Paresseux : `All` vit dans l'autre fichier partiel, l'ordre d'initialisation n'est pas garanti.
    private static readonly Lazy<Dictionary<int, StatusDefinition>> ById = new(() => All!.ToDictionary(s => s.Id));

    public static StatusDefinition? Find(int id) => ById.Value.GetValueOrDefault(id);

    public static bool IsLocked(int statusId) => LockedStatusIds.Contains(statusId);

    public static IReadOnlyList<int> AllowedTransitions(int statusId) => Nominal.TryGetValue(statusId, out var targets) ? targets : [];

    public static bool CanTransition(int from, int to) => AllowedTransitions(from).Contains(to);

    /// <summary>Lignes de `parcel_transitions` : nominales, contestations des terminaux, réception de retour.</summary>
    public static IEnumerable<(int From, int To, string Kind)> TransitionRows()
    {
        foreach (var (from, to) in ReturnReception) yield return (from, to, "RETURN_RECEPTION");
        foreach (var status in All)
        {
            foreach (var target in AllowedTransitions(status.Id)) yield return (status.Id, target, "NOMINAL");
            if (!IsLocked(status.Id)) continue;
            foreach (var target in DisputeTargets.Where(t => t != status.Id)) yield return (status.Id, target, "DISPUTE");
        }
    }
}
