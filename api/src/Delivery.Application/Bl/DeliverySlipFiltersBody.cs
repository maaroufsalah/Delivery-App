using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Bl;

public sealed record DeliverySlipFiltersBody
{
    public string? Search { get; init => field = Zod.Trim(value); }
    public string? Status { get; init; }
    public string? ClientId { get; init => field = Zod.Trim(value); }
    public string? CourierId { get; init => field = Zod.Trim(value); }
    public string? From { get; init => field = Zod.Trim(value); }
    public string? To { get; init => field = Zod.Trim(value); }
}
