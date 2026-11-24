using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Idempotency;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.PettyCash;

/// <summary>`pettyCashListFilterSchema` (catégorie, acheteur et statut multiples).</summary>
public sealed record PettyCashFilter(ListQuery Paging, string Period, string? From, string? To, string? AgencyId, string? Kind, IReadOnlyList<string>? Category, IReadOnlyList<string>? BuyerId,
    IReadOnlyList<string>? Status);
