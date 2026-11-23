using Delivery.Domain.Common;

namespace Delivery.Domain.Entities;

public partial class Agency
{
    public string Id { get; set; } = Cuid.New();
    public string Name { get; set; } = string.Empty;
    public string CityId { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? LogoUrl { get; set; }
    public string? Currency { get; set; }
    public string? Timezone { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public City City { get; set; } = null!;
    public ICollection<User> Users { get; set; } = [];
    public ICollection<UserAgency> UserLinks { get; set; } = [];
    public ICollection<AgencyCity> CityLinks { get; set; } = [];
    public ICollection<Client> Clients { get; set; } = [];
    public ICollection<Courier> Couriers { get; set; } = [];
    public ICollection<Parcel> Parcels { get; set; } = [];
    public ICollection<DeliveryNote> DeliveryNotes { get; set; } = [];
    public ICollection<ReturnHandover> ReturnHandovers { get; set; } = [];
    public ICollection<ReturnSlip> ReturnSlips { get; set; } = [];
    public ICollection<Invoice> Invoices { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
    public ICollection<CashRemittance> CashRemittances { get; set; } = [];
    public ICollection<CourierCashSession> CashSessions { get; set; } = [];
    public ICollection<CodPayout> CodPayouts { get; set; } = [];
    public ICollection<CourierStatement> CourierStatements { get; set; } = [];
    public ICollection<TransferOrder> TransferOrders { get; set; } = [];
    public ICollection<PettyCashMovement> PettyCashMovements { get; set; } = [];
    public ICollection<PettyCashDay> PettyCashDays { get; set; } = [];
    public ICollection<StockItem> StockItems { get; set; } = [];
    public ICollection<StockMovement> StockMovements { get; set; } = [];
    public ICollection<StorageRequest> StorageRequests { get; set; } = [];
    public ICollection<Order> Orders { get; set; } = [];
    public ICollection<BlNote> BlNotes { get; set; } = [];
}
