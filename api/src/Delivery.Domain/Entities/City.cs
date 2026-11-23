using Delivery.Domain.Common;

namespace Delivery.Domain.Entities;

public partial class City
{
    public string Id { get; set; } = Cuid.New();
    public string ZoneId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string? DeliveryDelay { get; set; }
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Zone Zone { get; set; } = null!;
    public ICollection<ZoneCity> ZoneLinks { get; set; } = [];
    public ICollection<ClientPickupAddress> PickupAddresses { get; set; } = [];
    public ICollection<AgencyCity> AgencyCities { get; set; } = [];
    public ICollection<CourierCity> CourierCities { get; set; } = [];
    public ICollection<ClientDiscountCity> DiscountLinks { get; set; } = [];
    public ICollection<District> Districts { get; set; } = [];
    public ICollection<Agency> Agencies { get; set; } = [];
    public ICollection<Client> Clients { get; set; } = [];
    public ICollection<Courier> Couriers { get; set; } = [];
    public ICollection<PricingRule> OriginRules { get; set; } = [];
    public ICollection<PricingRule> DestRules { get; set; } = [];
    public ICollection<Parcel> OriginParcels { get; set; } = [];
    public ICollection<Parcel> DestParcels { get; set; } = [];
    public ICollection<Parcel> CurrentParcels { get; set; } = [];
    public ICollection<Route> Routes { get; set; } = [];
    public ICollection<Route> RouteOrigins { get; set; } = [];
    public ICollection<DeliveryNote> DeliveryNotes { get; set; } = [];
    public ICollection<CashRemittance> CashRemittances { get; set; } = [];
    public ICollection<ParcelEvent> ParcelEvents { get; set; } = [];
    public ICollection<Order> Orders { get; set; } = [];
}
