using System.Text.Json.Nodes;
using AutoMapper;
using Delivery.Application.Clients.Dtos;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.Users;
using Delivery.Application.Users.Commands;
using Delivery.Application.Users.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Clients.Commands;

/// <summary>`PATCH /clients/:id/pickup-terms` : frais de ramassage (centimes) et seuil de gratuité propres au client.</summary>
public sealed record SetClientPickupTermsCommand : IRequest<ClientDetailDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Id { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasPickupFee { get; private init; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasFreePickupThreshold { get; private init; }

    public double? PickupFee
    {
        get;
        init
        {
            field = value;
            HasPickupFee = true;
        }
    }

    public double? FreePickupThreshold
    {
        get;
        init
        {
            field = value;
            HasFreePickupThreshold = true;
        }
    }
}

public sealed class SetClientPickupTermsValidator : ZodValidator<SetClientPickupTermsCommand>
{
    public SetClientPickupTermsValidator()
    {
        RuleFor(x => x.PickupFee).Must((x, v) => x.HasPickupFee).WithMessage(Zod.Expected("number"));
        RuleFor(x => x.PickupFee).Int().Min(0, "negative").Max(100_000_000, "amount-too-large");
        RuleFor(x => x.FreePickupThreshold).Must((x, v) => x.HasFreePickupThreshold).WithMessage(Zod.Expected("number"));
        RuleFor(x => x.FreePickupThreshold).Int().Min(0, "negative").Max(1000, "too-large");
    }
}

internal sealed class SetClientPickupTermsHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<SetClientPickupTermsCommand, ClientDetailDto>
{
    public async Task<ClientDetailDto> Handle(SetClientPickupTermsCommand request, CancellationToken cancellationToken)
    {
        var client = await ClientRows.FindAsync(db, request.Id, cancellationToken);
        client.PickupFee = request.PickupFee is null ? null : Money.FromCents((long)request.PickupFee.Value);
        client.FreePickupThreshold = request.FreePickupThreshold is null ? null : (int)request.FreePickupThreshold.Value;
        await db.SaveChangesAsync(cancellationToken);
        return await ClientRows.DetailAsync(db, caller, mapper, request.Id, cancellationToken);
    }
}
