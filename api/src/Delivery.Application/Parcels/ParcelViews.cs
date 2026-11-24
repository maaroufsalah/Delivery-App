using AutoMapper;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Parcels.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;

namespace Delivery.Application.Parcels;

/// <summary>Ligne EF d'un colis (`LIST_INCLUDE`), avant conversion « fil ».</summary>
public sealed class ParcelRow
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? ExternalCode { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string? CourierId { get; set; }
    public string AgencyId { get; set; } = string.Empty;
    public int StatusId { get; set; }
    public DateTime StatusAt { get; set; }
    public BillingState BillingState { get; set; }
    public FinancialStatus FinancialStatus { get; set; }
    public string OriginCityId { get; set; } = string.Empty;
    public string DestinationCityId { get; set; } = string.Empty;
    public string? DestinationDistrictId { get; set; }
    public string? DestinationDistrictName { get; set; }
    public DeliveryMode DeliveryMode { get; set; }
    public string CurrentCityId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientPhone { get; set; } = string.Empty;
    public string PickupAddress { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public decimal CodAmount { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal? FailureFee { get; set; }
    public decimal OtherFees { get; set; }
    public bool FeeIncluded { get; set; }
    public bool AlreadyPaid { get; set; }
    public bool PickupWaived { get; set; }
    public ParcelKind Kind { get; set; }
    public bool IsReturn { get; set; }
    public bool IsFailed { get; set; }
    public string? ReferenceParcelId { get; set; }
    public bool OpenOnDelivery { get; set; }
    public bool Fragile { get; set; }
    public bool BlRequested { get; set; }
    public int Quantity { get; set; }
    public ExpectedPayment? ExpectedPayment { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public bool ReimbursedByCourier { get; set; }
    public DateTime? ReimbursedAt { get; set; }
    public string? ReimbursedRemittanceId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string? CourierFirstName { get; set; }
    public string? CourierLastName { get; set; }
    public string OriginCityName { get; set; } = string.Empty;
    public string DestinationCityName { get; set; } = string.Empty;
    public string? DestinationDistrictRefName { get; set; }
}

public sealed class ParcelsProfile : Profile
{
    public ParcelsProfile()
    {
        CreateProjection<Parcel, ParcelRow>()
            .ForMember(d => d.ClientName, o => o.MapFrom(s => s.Client.Name))
            .ForMember(d => d.CourierFirstName, o => o.MapFrom(s => s.Courier != null ? s.Courier.FirstName : null))
            .ForMember(d => d.CourierLastName, o => o.MapFrom(s => s.Courier != null ? s.Courier.LastName : null))
            .ForMember(d => d.OriginCityName, o => o.MapFrom(s => s.OriginCity.Name))
            .ForMember(d => d.DestinationCityName, o => o.MapFrom(s => s.DestinationCity.Name))
            .ForMember(d => d.DestinationDistrictRefName, o => o.MapFrom(s => s.DestinationDistrict != null ? s.DestinationDistrict.Name : null));
    }
}

/// <summary>`toWireParcel`, `toListItem`, `parcelKindOf` de `ParcelsService`.</summary>
public static class ParcelViews
{
    public static string KindOf(ParcelKind kind) => kind switch
    {
        ParcelKind.RETURN => "return",
        ParcelKind.EXCHANGE => "exchange",
        _ => "parcel",
    };

    public static ParcelKind? KindDb(string kind) => kind switch
    {
        "parcel" => ParcelKind.STANDARD,
        "return" => ParcelKind.RETURN,
        "exchange" => ParcelKind.EXCHANGE,
        _ => null,
    };

    public static T Fill<T>(T dto, ParcelRow row, ICurrentUser caller)
        where T : ParcelDto
    {
        dto.Id = row.Id;
        dto.Code = row.Code;
        dto.ExternalCode = row.ExternalCode;
        dto.ClientId = row.ClientId;
        dto.CourierId = caller.ClientId is not null ? null : row.CourierId;
        dto.AgencyId = row.AgencyId;
        dto.StatusId = row.StatusId;
        dto.StatusAt = row.StatusAt;
        dto.BillingState = Money.ToWire(row.BillingState);
        dto.OriginCityId = row.OriginCityId;
        dto.DestinationCityId = row.DestinationCityId;
        dto.DestinationDistrictId = row.DestinationDistrictId;
        dto.DeliveryTarget = Money.ToWire(row.DeliveryMode);
        dto.CurrentCityId = row.CurrentCityId;
        dto.Description = row.Description;
        dto.RecipientName = row.RecipientName;
        dto.RecipientPhone = row.RecipientPhone;
        dto.PickupAddress = row.PickupAddress;
        dto.DeliveryAddress = row.DeliveryAddress;
        dto.CodAmount = Money.ToCents(row.CodAmount);
        dto.DeliveryFee = Money.ToCents(row.DeliveryFee);
        dto.FailureFee = Money.ToCentsOrNull(row.FailureFee);
        dto.OtherFees = Money.ToCents(row.OtherFees);
        dto.FeeIncluded = row.FeeIncluded;
        dto.AlreadyPaid = row.AlreadyPaid;
        dto.PickupWaived = row.PickupWaived;
        dto.Kind = KindOf(row.Kind);
        dto.IsReturn = row.IsReturn;
        dto.IsFailed = row.IsFailed;
        dto.ReferenceParcelId = row.ReferenceParcelId;
        dto.OpenOnDelivery = row.OpenOnDelivery;
        dto.Fragile = row.Fragile;
        dto.BlRequested = row.BlRequested;
        dto.Quantity = row.Quantity;
        dto.ExpectedPayment = row.ExpectedPayment is { } expected ? Money.ToWire(expected) : null;
        dto.ScheduledAt = row.ScheduledAt;
        dto.CreatedAt = row.CreatedAt;
        dto.UpdatedAt = row.UpdatedAt;
        var financial = FinancialStatuses.Wire(row.FinancialStatus);
        if (caller.CanSeeInternalCash())
        {
            dto.Extra["financialStatus"] = financial;
            dto.Extra["reimbursedByCourier"] = row.ReimbursedByCourier;
            dto.Extra["reimbursedAt"] = row.ReimbursedAt;
            dto.Extra["reimbursedRemittanceId"] = row.ReimbursedRemittanceId;
        }
        else
        {
            dto.Extra["merchantFinancialStatus"] = FinancialStatuses.MerchantOf(financial);
        }
        return dto;
    }

    public static ParcelDto ToWire(ParcelRow row, ICurrentUser caller) => Fill(new ParcelDto(), row, caller);

    public static ParcelListItemDto ToListItem(ParcelRow row, ICurrentUser caller) => Label(Fill(new ParcelListItemDto(), row, caller), row);

    public static T Label<T>(T item, ParcelRow row)
        where T : ParcelListItemDto
    {
        item.ClientName = row.ClientName;
        item.CourierName = row.CourierFirstName is null ? null : $"{row.CourierFirstName} {row.CourierLastName}";
        item.OriginCityName = row.OriginCityName;
        item.DestinationCityName = row.DestinationCityName;
        item.DestinationDistrictName = DistrictLabel(row.DestinationDistrictName, row.DestinationDistrictRefName);
        return item;
    }

    /// <summary>`districtLabel` : le quartier saisi, sinon celui du référentiel.</summary>
    public static string? DistrictLabel(string? typed, string? referential)
    {
        var text = typed?.Trim();
        if (!string.IsNullOrEmpty(text)) return text;
        var fallback = referential?.Trim();
        return string.IsNullOrEmpty(fallback) ? null : fallback;
    }
}
