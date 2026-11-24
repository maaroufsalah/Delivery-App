using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.Parcels.Commands;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.DeliveryNotes;

/// <summary>`deliveryNoteSchema`.</summary>
public sealed record CreateNoteCommand : IRequest<DeliveryNoteDetailDto>
{
    public string? Kind { get; init; } = "pickup";
    public string? ClientId { get; init; }
    public string? AgencyId { get; init; }
    public string? LocationCityId { get; init; }
}

public sealed class CreateNoteValidator : ZodValidator<CreateNoteCommand>
{
    public CreateNoteValidator()
    {
        RuleFor(x => x.Kind).OneOf(["pickup", "dropoff"]);
        RuleFor(x => x.ClientId).Required().MinLength(1, "required");
        RuleFor(x => x.AgencyId).Required().MinLength(1, "required");
        RuleFor(x => x.LocationCityId).Must(v => v is null || v.Length >= 0).WithMessage(Zod.InvalidInput);
    }
}

internal sealed class CreateNoteHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<CreateNoteCommand, DeliveryNoteDetailDto>
{
    public async Task<DeliveryNoteDetailDto> Handle(CreateNoteCommand input, CancellationToken cancellationToken)
    {
        var code = await NoteCreation.CreateAsync(db, caller, input.Kind == "dropoff" ? DeliveryNoteType.DROPOFF : DeliveryNoteType.PICKUP, input.ClientId!, input.AgencyId!, input.LocationCityId, null, cancellationToken);
        return await NoteRows.DetailAsync(db, caller, code, cancellationToken);
    }
}
