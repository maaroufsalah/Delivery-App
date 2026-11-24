using System.Text.Json.Serialization;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Staff;

public sealed record StaffAlertGroupDto<T>(int Count, IReadOnlyList<T> Items);

public sealed record StuckParcelAlertDto(string Code, string RecipientName, int Attempts, int StatusId);

public sealed record StaleRouteAlertDto(string Code, string CourierName, string CityName, long HoursOpen);

public sealed record PendingNoteAlertDto(string Code, string ClientName, string CityName, int ParcelCount, DateTime CreatedAt);

public sealed record RemittanceAlertDto(string Code, string CourierName, string CityName, long Amount, long Variance, string? VarianceReason, DateTime CreatedAt);

public sealed record ClaimAlertDto(string Code, string Subject, string ClientName, bool IsLate, DateTime CreatedAt);

/// <summary>`StaffAlerts` : les groupes que le compte a le droit de lire (les autres sont absents).</summary>
public sealed record StaffAlertsDto(StaffAlertGroupDto<StuckParcelAlertDto> StuckInRetry, StaffAlertGroupDto<StaleRouteAlertDto> StaleRoutes, string? AgencyId)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public StaffAlertGroupDto<PendingNoteAlertDto>? PendingNotes { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public StaffAlertGroupDto<RemittanceAlertDto>? PendingRemittances { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public StaffAlertGroupDto<RemittanceAlertDto>? CashVariances { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public StaffAlertGroupDto<ClaimAlertDto>? OpenClaims { get; init; }
}
