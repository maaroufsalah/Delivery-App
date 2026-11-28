namespace Delivery.Domain.Entities;

/// <summary>
/// Reprise de Wimo Laravel : une ligne par enregistrement repris (sorte, identifiant Laravel → identifiant .NET).
/// <see cref="Hash"/> est l'empreinte de la ligne source au dernier import : une relance ne réécrit que ce qui a changé.
/// </summary>
public partial class LegacyMapping
{
    public string Entity { get; set; } = string.Empty;
    public string LegacyId { get; set; } = string.Empty;
    public string NewId { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public DateTime ImportedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
