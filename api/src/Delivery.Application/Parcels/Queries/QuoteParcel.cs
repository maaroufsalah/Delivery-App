using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Pricing;
using FluentValidation;
using MediatR;

namespace Delivery.Application.Parcels.Queries;

/// <summary>`POST /parcels/quote` : le tarif d'un trajet (le client du vendeur connecté s'impose).</summary>
public sealed record QuoteParcelQuery : IRequest<Quote>
{
    public string? OriginCityId { get; init; }

    public string? DestinationCityId { get; init; }

    public string? DeliveryTarget { get; init; } = "district";

    public string? ClientId { get; init; }
}

public sealed class QuoteParcelValidator : ZodValidator<QuoteParcelQuery>
{
    public QuoteParcelValidator()
    {
        RuleFor(x => x.OriginCityId).Required().MinLength(1);
        RuleFor(x => x.DestinationCityId).Required().MinLength(1);
        RuleFor(x => x.DeliveryTarget).OneOf(ParcelInputs.DeliveryTargets);
    }
}

internal sealed class QuoteParcelHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<QuoteParcelQuery, Quote>
{
    public Task<Quote> Handle(QuoteParcelQuery request, CancellationToken cancellationToken) =>
        ParcelPricing.QuoteAsync(db, request.OriginCityId!, request.DestinationCityId!, request.DeliveryTarget ?? "district", caller.ClientId ?? request.ClientId, cancellationToken);
}
