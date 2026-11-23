using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class Parcel
{
    public string Id { get; set; } = Cuid.New();
    public string Code { get; set; } = string.Empty;
    public string? ExternalCode { get; set; }
    public string? IdempotencyKey { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string? CourierId { get; set; }
    public string AgencyId { get; set; } = string.Empty;
    public int StatusId { get; set; }
    public DateTime StatusAt { get; set; }
    public BillingState BillingState { get; set; } = BillingState.TO_INVOICE;
    public FinancialStatus FinancialStatus { get; set; } = FinancialStatus.NON_ENCAISSE;
    public string OriginCityId { get; set; } = string.Empty;
    public string DestinationCityId { get; set; } = string.Empty;
    public string? DestinationDistrictId { get; set; }
    public string? DestinationDistrictName { get; set; }
    public DeliveryMode DeliveryMode { get; set; } = DeliveryMode.DISTRICT;
    public string? DeliveryTypeId { get; set; }
    public string CurrentCityId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientPhone { get; set; } = string.Empty;
    public string PickupAddress { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public decimal CodAmount { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal? FailureFee { get; set; }
    public decimal OtherFees { get; set; } = 0m;
    public bool FeeIncluded { get; set; } = true;
    public bool AlreadyPaid { get; set; } = false;
    public bool PickupWaived { get; set; } = false;
    public decimal? CourierCommission { get; set; }
    public DateTime? CourierCommissionAt { get; set; }
    public CourierCommissionSource? CourierCommissionSource { get; set; }
    public string? CourierCommissionCourierId { get; set; }
    public bool ReimbursedByCourier { get; set; } = false;
    public DateTime? ReimbursedAt { get; set; }
    public string? ReimbursedRemittanceId { get; set; }
    public ParcelKind Kind { get; set; } = ParcelKind.STANDARD;
    public bool IsReturn { get; set; } = false;
    public bool IsFailed { get; set; } = false;
    public string? ReferenceParcelId { get; set; }
    public string? ImportId { get; set; }
    public bool OpenOnDelivery { get; set; } = false;
    public bool Fragile { get; set; } = false;
    public bool BlRequested { get; set; } = false;
    public int Quantity { get; set; } = 1;
    public ExpectedPayment? ExpectedPayment { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Client Client { get; set; } = null!;
    public Courier? Courier { get; set; }
    public Courier? CommissionCourier { get; set; }
    public Agency Agency { get; set; } = null!;
    public ParcelStatus Status { get; set; } = null!;
    public City OriginCity { get; set; } = null!;
    public City DestinationCity { get; set; } = null!;
    public District? DestinationDistrict { get; set; }
    public DeliveryType? DeliveryType { get; set; }
    public City CurrentCity { get; set; } = null!;
    public Parcel? ReferenceParcel { get; set; }
    public ICollection<Parcel> DerivedParcels { get; set; } = [];
    public ICollection<ParcelEvent> Events { get; set; } = [];
    public ICollection<ParcelNote> Notes { get; set; } = [];
    public ICollection<ParcelFeeChange> FeeChanges { get; set; } = [];
    public ICollection<ParcelItem> Items { get; set; } = [];
    public ICollection<RouteParcel> RouteLinks { get; set; } = [];
    public ICollection<DeliveryNoteParcel> NoteLinks { get; set; } = [];
    public ICollection<DeliveryNoteRemovedParcel> NoteRemovals { get; set; } = [];
    public ICollection<ReturnNoteParcel> ReturnLinks { get; set; } = [];
    public ICollection<ReturnHandoverItem> HandoverItems { get; set; } = [];
    public ICollection<ReturnSlipItem> SlipItems { get; set; } = [];
    public DeliverySlip? DeliverySlip { get; set; }
    public ICollection<StockMovement> StockMovements { get; set; } = [];
    public Order? Order { get; set; }
    public ParcelImport? Import { get; set; }
    public ICollection<Claim> Claims { get; set; } = [];
    public ICollection<InvoiceLine> InvoiceLines { get; set; } = [];
    public ICollection<CashRemittanceParcel> Remittances { get; set; } = [];
    public ICollection<CourierCashSessionParcel> CashSessionLinks { get; set; } = [];
    public Cheque? Cheque { get; set; }
    public ICollection<Exchange> ExchangesAsOriginal { get; set; } = [];
    public Exchange? ExchangeAsNew { get; set; }
    public ICollection<CourierStatementLine> StatementLines { get; set; } = [];
    public ICollection<CodPayoutParcel> CodPayouts { get; set; } = [];
    public ICollection<TransferOrderParcel> TransferOrders { get; set; } = [];
}
