using System.Text.Json;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.Returns;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Finance;

/// <summary>`remittanceCandidateQuerySchema` : les colis livrés et encaissés du livreur, pas encore remis.</summary>
public sealed record GetRemittanceCandidatesQuery(string? CourierId, string? CityId, string? From, string? To, string? Search) : IRequest<IReadOnlyList<RemittanceCandidateDto>>;

public sealed class GetRemittanceCandidatesValidator : ZodValidator<GetRemittanceCandidatesQuery>
{
    public GetRemittanceCandidatesValidator()
    {
        RuleFor(x => x.CourierId).Required().MinLength(1, "required");
        RuleFor(x => x.Search).MaxLength(120);
    }
}

internal sealed class GetRemittanceCandidatesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetRemittanceCandidatesQuery, IReadOnlyList<RemittanceCandidateDto>>
{
    public async Task<IReadOnlyList<RemittanceCandidateDto>> Handle(GetRemittanceCandidatesQuery request, CancellationToken cancellationToken)
    {
        var where = ParcelFilters.Scoped(db.Parcels.AsNoTracking(), caller)
            .Where(p => p.StatusId == Billing.DeliveredStatusId && p.FinancialStatus != FinancialStatus.NON_ENCAISSE && !p.Remittances.Any(l => l.ConfirmedParcelId != null));
        var courierId = caller.CourierId ?? (caller.MayFilterBy(caller.CourierId) ? request.CourierId : null);
        if (courierId is not null) where = where.Where(p => p.CourierId == courierId);
        if (!string.IsNullOrEmpty(request.CityId)) where = where.Where(p => p.DestinationCityId == request.CityId);
        var (from, to) = ReturnRows.DayRange(request.From, request.To);
        if (from is { } f) where = where.Where(p => p.StatusAt >= f);
        if (to is { } t) where = where.Where(p => p.StatusAt < t);
        if (!string.IsNullOrEmpty(request.Search))
        {
            var needle = request.Search.ToLower();
            where = where.Where(p => p.Code.ToLower().Contains(needle) || p.RecipientName.ToLower().Contains(needle));
        }
        var rows = await where.OrderBy(p => p.StatusAt).Take(NoteRules.SelectionMax + 1)
            .Select(p => new { p.Id, p.Code, Client = p.Client.Name, p.RecipientName, City = p.DestinationCity.Name, p.CodAmount, p.DeliveryFee, p.FeeIncluded, p.AlreadyPaid, p.CourierCommission, p.StatusAt })
            .ToListAsync(cancellationToken);
        return rows.Select(p => new RemittanceCandidateDto(p.Id, p.Code, p.Client, p.RecipientName, p.City, Remittances.Collected(p.CodAmount, p.DeliveryFee, p.FeeIncluded, p.AlreadyPaid),
            Money.ToCents(p.CourierCommission ?? 0m), p.StatusAt)).Where(c => c.CodCollected > 0).ToList();
    }
}
