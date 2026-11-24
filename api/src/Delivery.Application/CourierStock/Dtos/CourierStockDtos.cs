using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using Delivery.Domain.Parcels;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.CourierStock;

public sealed class CourierStockRowDto : CourierStockCounters
{
    [JsonPropertyOrder(1)] public string CourierId { get; set; } = string.Empty;
    [JsonPropertyOrder(1)] public string CourierName { get; set; } = string.Empty;
    [JsonPropertyOrder(1)] public string CourierPhone { get; set; } = string.Empty;
    [JsonPropertyOrder(1)] public string? AgencyId { get; set; }
    [JsonPropertyOrder(1)] public string? AgencyName { get; set; }
}

public sealed record CourierStockPageDto(IReadOnlyList<CourierStockRowDto> Items, int Total, int Page, int PageSize, CourierStockCounters Totals);

public sealed record CourierStockParcelDto(string Id, string Code, string RecipientName, string RecipientPhone, string DestinationCityId, string DestinationCityName, string ClientId,
    string ClientName, long CodInCents, int StatusId, string Block, string? RouteId, string? RouteCode, DateTime? AssignedAt, DateTime? LastMovementAt);

public sealed record CourierStockStatusLineDto(int StatusId, int Count, long CodInCents);

public sealed record CourierStockOptionDto(string Value, string Label);

public sealed record CourierStockOptionsDto(IReadOnlyList<int> Statuses, IReadOnlyList<CourierStockOptionDto> Clients, IReadOnlyList<CourierStockOptionDto> Cities,
    IReadOnlyList<CourierStockOptionDto> Routes);

public sealed record CourierStockCourierDto(string Id, string Name, string Phone, string? AgencyId, string? AgencyName);

public sealed record CourierStockParcelPageDto(IReadOnlyList<CourierStockParcelDto> Items, int Total, int Page, int PageSize);

public sealed record CourierStockDetailDto(CourierStockCourierDto Courier, CourierStockCounters Counters, IReadOnlyList<CourierStockStatusLineDto> ByStatus, CourierStockParcelPageDto Parcels,
    CourierStockOptionsDto Options);
