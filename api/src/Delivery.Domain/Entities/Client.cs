using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class Client
{
    public string Id { get; set; } = Cuid.New();
    public string? AgencyId { get; set; }
    public string? UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? LegalName { get; set; }
    public ClientType Type { get; set; } = ClientType.MERCHANT;
    public string? TaxId { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string CityId { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPro { get; set; } = false;
    public BillingType BillingType { get; set; } = BillingType.NORMAL;
    public bool MonthlyBilling { get; set; } = false;
    public int? VatRate { get; set; }
    public decimal? PickupFee { get; set; }
    public int? FreePickupThreshold { get; set; }
    public DateTime? ValidatedAt { get; set; }
    public string? ValidatedById { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectedById { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Agency? Agency { get; set; }
    public User? User { get; set; }
    public City City { get; set; } = null!;
    public User? ValidatedBy { get; set; }
    public User? RejectedBy { get; set; }
    public ICollection<ClientDiscount> Discounts { get; set; } = [];
    public ICollection<ClientPickupAddress> PickupAddresses { get; set; } = [];
    public ICollection<ClientDocument> Documents { get; set; } = [];
    public ICollection<Parcel> Parcels { get; set; } = [];
    public ICollection<DeliveryNote> DeliveryNotes { get; set; } = [];
    public ICollection<ReturnNote> ReturnNotes { get; set; } = [];
    public ICollection<ReturnSlip> ReturnSlips { get; set; } = [];
    public ICollection<DeliverySlip> DeliverySlips { get; set; } = [];
    public ICollection<BlNote> BlNotes { get; set; } = [];
    public ICollection<Invoice> Invoices { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
    public ICollection<CodPayout> CodPayouts { get; set; } = [];
    public ICollection<Product> Products { get; set; } = [];
    public ICollection<StockItem> StockItems { get; set; } = [];
    public ICollection<StorageRequest> StorageRequests { get; set; } = [];
    public ICollection<Order> Orders { get; set; } = [];
    public ICollection<Claim> Claims { get; set; } = [];
    public ICollection<ParcelImport> ParcelImports { get; set; } = [];
}
