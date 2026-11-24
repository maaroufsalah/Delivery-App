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

public sealed record PettyCashMovementDto(string Id, string Kind, string Date, long Amount, string AgencyId, string AgencyName, string? Label, string? Category, string? BuyerId,
    string? BuyerName, string? Note, string? ProofName, string? CreatedByName, DateTime CreatedAt, DateTime UpdatedAt, string Status, string? RefusalReason, string? DecidedByName,
    DateTime? DecidedAt, string DayStatus);

public sealed record PettyCashTotalsDto(long Balance, long Supplied, long Spent, int Drafts);

public sealed record PettyCashPageDto(IReadOnlyList<PettyCashMovementDto> Items, int Total, int Page, int PageSize, PettyCashTotalsDto Totals, string Period, string From, string To);

public sealed record PettyCashPersonDto(string Id, string Name);

public sealed record PettyCashAgencyDto(string Id, string Name);

public sealed record PettyCashMetaDto(IReadOnlyList<string> Categories, IReadOnlyList<PettyCashPersonDto> Buyers, IReadOnlyList<PettyCashAgencyDto> Agencies);

public sealed record PettyCashCourierPageDto(IReadOnlyList<PettyCashMovementDto> Items, int Total, int Page, int PageSize, int Drafts, IReadOnlyList<string> Categories);

/// <summary>`pettyCashProofSchema` : un justificatif joint (PDF ou image), en base64.</summary>
public sealed record ProofFileDto
{
    public string? Name { get; init => field = Zod.Trim(value); }
    public string? MimeType { get; init; }
    public string? Content { get; init; }
}

public sealed record PettyCashSweepDto(int Created, int Closed);
