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

/// <summary>`blCollectSchema` : le livreur récupère (ou non) le BL signé à la livraison.</summary>
public sealed record CollectBlCommand : IRequest<ParcelBlTrackingDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public bool? Collected { get; init; }
    public string? Reference { get; init => field = Zod.Trim(value); }
}

public sealed class CollectBlValidator : ZodValidator<CollectBlCommand>
{
    public CollectBlValidator()
    {
        RuleFor(x => x.Collected).Required("boolean");
        RuleFor(x => x.Reference).MaxLength(60);
    }
}

internal sealed class CollectBlHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<CollectBlCommand, ParcelBlTrackingDto>
{
    public async Task<ParcelBlTrackingDto> Handle(CollectBlCommand input, CancellationToken cancellationToken)
    {
        await BlCollection.CollectAsync(db, caller, input.Code, input.Collected == true, input.Reference, cancellationToken);
        return await BlRows.TrackingAsync(db, caller, input.Code, cancellationToken);
    }
}
