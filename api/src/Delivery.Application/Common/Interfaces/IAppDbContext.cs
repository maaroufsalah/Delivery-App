using Delivery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Delivery.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<Company> Companies { get; }
    DbSet<Agency> Agencies { get; }
    DbSet<User> Users { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<UserAgency> UserAgencies { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserPermissionOverride> UserPermissionOverrides { get; }
    DbSet<Zone> Zones { get; }
    DbSet<ZoneCity> ZoneCities { get; }
    DbSet<ZoneRate> ZoneRates { get; }
    DbSet<DeliveryType> DeliveryTypes { get; }
    DbSet<City> Cities { get; }
    DbSet<District> Districts { get; }
    DbSet<PricingRule> PricingRules { get; }
    DbSet<Client> Clients { get; }
    DbSet<ClientDiscount> ClientDiscounts { get; }
    DbSet<ClientDiscountCity> ClientDiscountCities { get; }
    DbSet<ClientPickupAddress> ClientPickupAddresses { get; }
    DbSet<ClientDocument> ClientDocuments { get; }
    DbSet<Courier> Couriers { get; }
    DbSet<AgencyCity> AgencyCities { get; }
    DbSet<CourierCity> CourierCities { get; }
    DbSet<ParcelStatus> ParcelStatuses { get; }
    DbSet<ParcelTransition> ParcelTransitions { get; }
    DbSet<Parcel> Parcels { get; }
    DbSet<ParcelEvent> ParcelEvents { get; }
    DbSet<ParcelNote> ParcelNotes { get; }
    DbSet<ParcelFeeChange> ParcelFeeChanges { get; }
    DbSet<ParcelCodeSequence> ParcelCodeSequences { get; }
    DbSet<ParcelImport> ParcelImports { get; }
    DbSet<LabelJob> LabelJobs { get; }
    DbSet<BulkJob> BulkJobs { get; }
    DbSet<Route> Routes { get; }
    DbSet<RouteParcel> RouteParcels { get; }
    DbSet<DeliveryNote> DeliveryNotes { get; }
    DbSet<RouteNote> RouteNotes { get; }
    DbSet<DeliveryNoteParcel> DeliveryNoteParcels { get; }
    DbSet<DeliveryNoteRemovedParcel> DeliveryNoteRemovedParcels { get; }
    DbSet<ReturnNote> ReturnNotes { get; }
    DbSet<ReturnNoteParcel> ReturnNoteParcels { get; }
    DbSet<DocumentSequence> DocumentSequences { get; }
    DbSet<ReturnHandover> ReturnHandovers { get; }
    DbSet<ReturnHandoverEvent> ReturnHandoverEvents { get; }
    DbSet<ReturnHandoverItem> ReturnHandoverItems { get; }
    DbSet<ReturnSlip> ReturnSlips { get; }
    DbSet<ReturnSlipItem> ReturnSlipItems { get; }
    DbSet<DeliverySlip> DeliverySlips { get; }
    DbSet<DeliverySlipEvent> DeliverySlipEvents { get; }
    DbSet<BlNote> BlNotes { get; }
    DbSet<BlNoteItem> BlNoteItems { get; }
    DbSet<BlNoteEvent> BlNoteEvents { get; }
    DbSet<Bank> Banks { get; }
    DbSet<BankAccount> BankAccounts { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<InvoiceLine> InvoiceLines { get; }
    DbSet<InvoiceAdjustment> InvoiceAdjustments { get; }
    DbSet<Payment> Payments { get; }
    DbSet<CashRemittance> CashRemittances { get; }
    DbSet<CashRemittanceParcel> CashRemittanceParcels { get; }
    DbSet<CodPayout> CodPayouts { get; }
    DbSet<CodPayoutParcel> CodPayoutParcels { get; }
    DbSet<CourierCashSession> CourierCashSessions { get; }
    DbSet<CourierCashSessionParcel> CourierCashSessionParcels { get; }
    DbSet<CourierBalanceEntry> CourierBalanceEntries { get; }
    DbSet<Exchange> Exchanges { get; }
    DbSet<ExchangeEvent> ExchangeEvents { get; }
    DbSet<Cheque> Cheques { get; }
    DbSet<ChequeEvent> ChequeEvents { get; }
    DbSet<CourierStatement> CourierStatements { get; }
    DbSet<CourierStatementLine> CourierStatementLines { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductAttribute> ProductAttributes { get; }
    DbSet<ProductAttributeValue> ProductAttributeValues { get; }
    DbSet<ProductAttributeAssignment> ProductAttributeAssignments { get; }
    DbSet<StockItem> StockItems { get; }
    DbSet<StockMovement> StockMovements { get; }
    DbSet<ParcelItem> ParcelItems { get; }
    DbSet<StorageRequest> StorageRequests { get; }
    DbSet<StorageRequestItem> StorageRequestItems { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderLine> OrderLines { get; }
    DbSet<ClaimType> ClaimTypes { get; }
    DbSet<Claim> Claims { get; }
    DbSet<ClaimEvent> ClaimEvents { get; }
    DbSet<ClaimAttachment> ClaimAttachments { get; }
    DbSet<Announcement> Announcements { get; }
    DbSet<PlatformAuditLog> PlatformAuditLogs { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<ServerError> ServerErrors { get; }
    DbSet<FieldChange> FieldChanges { get; }
    DbSet<Setting> Settings { get; }
    DbSet<PushToken> PushTokens { get; }
    DbSet<NotificationPreference> NotificationPreferences { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<PasswordResetRequest> PasswordResetRequests { get; }
    DbSet<PettyCashMovement> PettyCashMovements { get; }
    DbSet<PettyCashDay> PettyCashDays { get; }
    DbSet<TransferOrder> TransferOrders { get; }
    DbSet<TransferOrderParcel> TransferOrderParcels { get; }
    DbSet<IdempotentRequest> IdempotentRequests { get; }
    DbSet<RefreshSession> RefreshSessions { get; }
    DbSet<ProviderToken> ProviderTokens { get; }
    DbSet<ProviderCall> ProviderCalls { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
