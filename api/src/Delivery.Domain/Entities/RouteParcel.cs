using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class RouteParcel
{
    public string RouteId { get; set; } = string.Empty;
    public string ParcelId { get; set; } = string.Empty;
    public int Sequence { get; set; } = 0;
    public bool Confirmed { get; set; } = false;
    public DateTime? ConfirmedAt { get; set; }
    public string? ConfirmedBy { get; set; }
    public decimal? CourierFee { get; set; }
    public int? FinalStatusId { get; set; }
    public decimal? CodCollected { get; set; }
    public decimal? CodCash { get; set; }
    public decimal? CodCheque { get; set; }
    public decimal? CourierCommission { get; set; }
    public CourierCommissionSource? CourierCommissionSource { get; set; }
    public string? Note { get; set; }
    public DateTime? CorrectedAt { get; set; }
    public int? CorrectedStatusId { get; set; }
    public Route Route { get; set; } = null!;
    public Parcel Parcel { get; set; } = null!;
}
