using System.Globalization;
using System.Text.Json;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Invoices;

public sealed record TransferOrderDetailDto(string Code, string Source, IReadOnlyList<TransferLineDto> Lines, IReadOnlyList<string> InvoiceCodes, long TotalCollected, long TotalFees,
    long TotalDue, DateTime CreatedAt, string? CreatedByName);
