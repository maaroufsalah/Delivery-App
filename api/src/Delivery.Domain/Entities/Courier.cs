using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class Courier
{
    public string Id { get; set; } = Cuid.New();
    public string? UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string CityId { get; set; } = string.Empty;
    public string? AgencyId { get; set; }
    public string? NationalId { get; set; }
    public VehicleType VehicleType { get; set; } = VehicleType.MOTORBIKE;
    public bool IsActive { get; set; } = true;
    public string? BankName { get; set; }
    public string? Rib { get; set; }
    public PayModel PayModel { get; set; } = PayModel.COMMISSION;
    public SettlementMode SettlementMode { get; set; } = SettlementMode.AT_REMITTANCE;
    public PayPeriod PayPeriod { get; set; } = PayPeriod.MONTHLY;
    public decimal? FixedSalary { get; set; }
    public DateTime? ValidatedAt { get; set; }
    public string? ValidatedById { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectedById { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public User? User { get; set; }
    public City City { get; set; } = null!;
    public Agency? Agency { get; set; }
    public User? ValidatedBy { get; set; }
    public User? RejectedBy { get; set; }
    public ICollection<CourierCity> Cities { get; set; } = [];
    public ICollection<Parcel> Parcels { get; set; } = [];
    public ICollection<Parcel> CommissionedParcels { get; set; } = [];
    public ICollection<Route> Routes { get; set; } = [];
    public ICollection<ReturnNote> ReturnNotes { get; set; } = [];
    public ICollection<ReturnHandover> ReturnHandovers { get; set; } = [];
    public ICollection<DeliverySlip> DeliverySlips { get; set; } = [];
    public ICollection<BlNote> BlNotes { get; set; } = [];
    public ICollection<CashRemittance> CashRemittances { get; set; } = [];
    public ICollection<CourierStatement> Statements { get; set; } = [];
    public ICollection<CourierCashSession> CashSessions { get; set; } = [];
    public ICollection<CourierBalanceEntry> BalanceEntries { get; set; } = [];
    public ICollection<Exchange> Exchanges { get; set; } = [];
    public ICollection<Cheque> Cheques { get; set; } = [];
    public ICollection<ProviderToken> ProviderTokens { get; set; } = [];
}
