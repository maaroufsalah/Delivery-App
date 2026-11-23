using Delivery.Domain.Common;
using Delivery.Domain.Enums;

namespace Delivery.Domain.Entities;

public partial class User
{
    public string Id { get; set; } = Cuid.New();
    public bool IsPlatformAdmin { get; set; } = false;
    public string? AgencyId { get; set; }
    public string? Email { get; set; }
    public string? Username { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public string? JobTitle { get; set; }
    public string? NationalId { get; set; }
    public bool IsActive { get; set; } = true;
    public UserStatus Status { get; set; } = UserStatus.ACTIVE;
    public DateTime TokensValidFrom { get; set; }
    public bool MustChangePassword { get; set; } = false;
    public DateTime? InvitedAt { get; set; }
    public string? InvitedById { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Agency? Agency { get; set; }
    public ICollection<UserRole> Roles { get; set; } = [];
    public ICollection<UserAgency> Agencies { get; set; } = [];
    public Client? Client { get; set; }
    public Courier? Courier { get; set; }
    public ICollection<ProviderToken> ProviderTokens { get; set; } = [];
    public User? InvitedBy { get; set; }
    public ICollection<User> InvitedUsers { get; set; } = [];
    public ICollection<Client> ValidatedClients { get; set; } = [];
    public ICollection<Courier> ValidatedCouriers { get; set; } = [];
    public ICollection<Client> RejectedClients { get; set; } = [];
    public ICollection<Courier> RejectedCouriers { get; set; } = [];
    public ICollection<ClientDocument> UploadedDocuments { get; set; } = [];
    public ICollection<ParcelEvent> ParcelEvents { get; set; } = [];
    public ICollection<ParcelNote> ParcelNotes { get; set; } = [];
    public ICollection<ParcelImport> ParcelImports { get; set; } = [];
    public ICollection<ParcelFeeChange> ParcelFeeChanges { get; set; } = [];
    public ICollection<AuditLog> AuditLogs { get; set; } = [];
    public ICollection<Claim> AssignedClaims { get; set; } = [];
    public ICollection<Claim> AuthoredClaims { get; set; } = [];
    public ICollection<ClaimEvent> ClaimEvents { get; set; } = [];
    public ICollection<ClaimAttachment> ClaimAttachments { get; set; } = [];
    public ICollection<Announcement> Announcements { get; set; } = [];
    public ICollection<PlatformAuditLog> PlatformActions { get; set; } = [];
    public ICollection<ReturnHandover> ConfirmedReturnHandovers { get; set; } = [];
    public ICollection<ReturnHandoverEvent> ReturnHandoverEvents { get; set; } = [];
    public ICollection<ReturnHandover> CreatedReturnHandovers { get; set; } = [];
    public ICollection<DeliverySlipEvent> DeliverySlipEvents { get; set; } = [];
    public ICollection<BlNote> CreatedBlNotes { get; set; } = [];
    public ICollection<BlNoteEvent> BlNoteEvents { get; set; } = [];
    public ICollection<ReturnSlip> DeliveredReturnSlips { get; set; } = [];
    public ICollection<Invoice> IssuedInvoices { get; set; } = [];
    public ICollection<FieldChange> FieldChanges { get; set; } = [];
    public ICollection<CourierCashSession> ValidatedCashSessions { get; set; } = [];
    public ICollection<Exchange> CreatedExchanges { get; set; } = [];
    public ICollection<ExchangeEvent> ExchangeEvents { get; set; } = [];
    public ICollection<ChequeEvent> ChequeEvents { get; set; } = [];
    public ICollection<CourierBalanceEntry> BalanceEntries { get; set; } = [];
    public ICollection<Invoice> PaidInvoices { get; set; } = [];
    public ICollection<CashRemittance> ConfirmedRemittances { get; set; } = [];
    public ICollection<CashRemittance> CourierClosedRemittances { get; set; } = [];
    public ICollection<CodPayout> PaidCodPayouts { get; set; } = [];
    public ICollection<TransferOrder> TransferOrders { get; set; } = [];
    public ICollection<PettyCashMovement> PettyCashPurchases { get; set; } = [];
    public ICollection<PettyCashMovement> PettyCashEntries { get; set; } = [];
    public ICollection<PettyCashMovement> PettyCashDecisions { get; set; } = [];
    public ICollection<PettyCashDay> PettyCashDaysClosed { get; set; } = [];
    public ICollection<PettyCashDay> PettyCashDaysReopened { get; set; } = [];
    public ICollection<CourierStatement> ValidatedStatements { get; set; } = [];
    public ICollection<CourierStatement> PaidStatements { get; set; } = [];
    public ICollection<StockMovement> StockMovements { get; set; } = [];
    public ICollection<StorageRequest> DecidedStorageRequests { get; set; } = [];
    public ICollection<PushToken> PushTokens { get; set; } = [];
    public ICollection<NotificationPreference> NotificationPreferences { get; set; } = [];
    public ICollection<Notification> Notifications { get; set; } = [];
    public ICollection<PasswordResetRequest> PasswordResetRequests { get; set; } = [];
    public ICollection<UserPermissionOverride> PermissionOverrides { get; set; } = [];
}
