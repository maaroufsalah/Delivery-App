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

/// <summary>`courierStockParcelQuerySchema`.</summary>
public sealed record CourierStockParcelFilter(int Page, int PageSize, string? Search, int? StatusId, IReadOnlyList<string>? ClientId, IReadOnlyList<string>? CityId,
    IReadOnlyList<string>? RouteId, string Sort, string Order);
