using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoMapper;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Application.Parcels.Dtos;
using Delivery.Application.Parcels.Queries;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Parcels.Commands;

/// <summary>`PATCH /parcels/:code/bl` : demander ou retirer le BL (Client Pro, BL pas encore récupéré).</summary>
public sealed record SetParcelBlRequestedCommand : IRequest<ParcelDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public bool? BlRequested { get; init; }
}

public sealed class SetParcelBlRequestedValidator : ZodValidator<SetParcelBlRequestedCommand>
{
    public SetParcelBlRequestedValidator()
    {
        RuleFor(x => x.BlRequested).Required("boolean");
    }
}

internal sealed class SetParcelBlRequestedHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<SetParcelBlRequestedCommand, ParcelDetailDto>
{
    public async Task<ParcelDetailDto> Handle(SetParcelBlRequestedCommand input, CancellationToken cancellationToken)
    {
        var requested = input.BlRequested!.Value;
        var parcel = await ParcelFilters.Scoped(db.Parcels, caller).Include(p => p.Client).Include(p => p.DeliverySlip)
                         .FirstOrDefaultAsync(p => p.Code == input.Code, cancellationToken)
                     ?? throw BusinessException.NotFound($"Colis introuvable : {input.Code}");
        if (requested && !parcel.Client.IsPro) throw BusinessException.Conflict("Le BL ne se demande que pour un Client Pro.", new { reason = "client-not-pro" });
        if (!requested && parcel.DeliverySlip is not null)
        {
            throw BusinessException.Conflict("Le BL de ce colis est déjà récupéré : la demande ne se retire plus.", new { reason = "bl-already-collected" });
        }
        parcel.BlRequested = requested;
        db.ParcelEvents.Add(new ParcelEvent
        {
            ParcelId = parcel.Id,
            StatusId = parcel.StatusId,
            Comment = requested ? "BL à récupérer : OUI" : "BL à récupérer : NON",
            AuthorId = caller.UserId,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
        return await GetParcelHandler.LoadAsync(db, caller, mapper, new GetParcelQuery(input.Code), cancellationToken);
    }
}
