using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Pricing;
using FluentValidation;
using MediatR;

namespace Delivery.Application.Parcels.Queries;

/// <summary>`GET /parcels/next-code` : le prochain numéro du trajet, sans le réserver.</summary>
public sealed record GetNextParcelCodeQuery : IRequest<NextParcelCodeDto>
{
    public string? OriginCityId { get; init; }

    public string? DestinationCityId { get; init; }
}

public sealed record NextParcelCodeDto(string Code);

public sealed class GetNextParcelCodeValidator : ZodValidator<GetNextParcelCodeQuery>
{
    public GetNextParcelCodeValidator()
    {
        RuleFor(x => x.OriginCityId).Required().MinLength(1);
        RuleFor(x => x.DestinationCityId).Required().MinLength(1);
    }
}

internal sealed class GetNextParcelCodeHandler(IAppDbContext db, IClock clock) : IRequestHandler<GetNextParcelCodeQuery, NextParcelCodeDto>
{
    public async Task<NextParcelCodeDto> Handle(GetNextParcelCodeQuery request, CancellationToken cancellationToken) =>
        new(await ParcelCodes.PreviewAsync(db, request.OriginCityId!, request.DestinationCityId!, clock.UtcNow, cancellationToken));
}
