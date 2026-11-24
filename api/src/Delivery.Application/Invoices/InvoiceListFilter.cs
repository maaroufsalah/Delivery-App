using System.Globalization;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Finance;
using Delivery.Application.Parcels;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using Delivery.Domain.Parcels;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Invoices;

/// <summary>`invoiceListQuerySchema` (+ `kind` : « normal », « pro » ou aucun).</summary>
public sealed record InvoiceListFilter(ListQuery Paging, IReadOnlyList<string>? Status, IReadOnlyList<string>? ClientId, IReadOnlyList<string>? AgencyId, bool UnpaidOnly, string? From, string? To,
    string? IssuedDay, string? Kind);
