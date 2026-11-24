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

/// <summary>`pettyCashDaysQuerySchema`.</summary>
public sealed record GetPettyCashDaysQuery(string? AgencyId, string? Status, string? WithDrafts, string? Limit) : IRequest<PettyCashDaysPageDto>;

internal sealed class GetPettyCashDaysHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<GetPettyCashDaysQuery, PettyCashDaysPageDto>
{
    public Task<PettyCashDaysPageDto> Handle(GetPettyCashDaysQuery request, CancellationToken cancellationToken)
    {
        var issues = new List<object>();
        if (request.Status is { } s && s is not ("open" or "closed")) issues.Add(new { path = "status", message = Zod.InvalidOption(["open", "closed"]) });
        if (request.WithDrafts is { } w && w is not ("true" or "false")) issues.Add(new { path = "withDrafts", message = Zod.InvalidOption(["true", "false"]) });
        var limit = 400;
        if (request.Limit is { } raw)
        {
            if (!int.TryParse(raw.Trim(), out limit) || limit < 1 || limit > 1000) issues.Add(new { path = "limit", message = "Invalid input" });
        }
        if (issues.Count > 0) throw BusinessException.Validation("Saisie invalide.", new { issues });
        return PettyCashDays.ListAsync(db, caller, clock, request.AgencyId, request.Status, request.WithDrafts == "true", limit, cancellationToken);
    }
}
