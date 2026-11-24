using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.CourierApp;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Application.Routes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using Delivery.Domain.Pricing;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Returns;

/// <summary>`pendingQuerySchema` : les colis à rapporter, par livreur ; pour un vendeur, ses retours.</summary>
public sealed record GetPendingReturnsQuery(string? AgencyId, string? CourierId, string? Search) : IRequest<IReadOnlyList<ReturnPendingGroupDto>>;

public sealed class GetPendingReturnsValidator : ZodValidator<GetPendingReturnsQuery>
{
    public GetPendingReturnsValidator()
    {
        RuleFor(x => x.Search).MaxLength(120);
    }
}

internal sealed class GetPendingReturnsHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetPendingReturnsQuery, IReadOnlyList<ReturnPendingGroupDto>>
{
    public Task<IReadOnlyList<ReturnPendingGroupDto>> Handle(GetPendingReturnsQuery request, CancellationToken cancellationToken) => PendingReturns.ListAsync(db, caller, request, cancellationToken);
}
