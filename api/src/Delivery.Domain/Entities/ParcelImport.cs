using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class ParcelImport
{
    public string Id { get; set; } = Cuid.New();
    public string ClientId { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;
    public string? StorageKey { get; set; }
    public ParcelImportMode Mode { get; set; } = ParcelImportMode.ALL_OR_NOTHING;
    public int RowCount { get; set; } = 0;
    public int OkCount { get; set; } = 0;
    public int KoCount { get; set; } = 0;
    public string Errors { get; set; } = "[]";
    public string? CreatedById { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Client Client { get; set; } = null!;
    public User? CreatedBy { get; set; }
    public ICollection<Parcel> Parcels { get; set; } = [];
}
