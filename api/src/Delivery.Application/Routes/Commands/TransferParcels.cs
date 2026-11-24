using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Delivery.Application.BulkJobs;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels.Commands;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Routes;

/// <summary>`parcelTransferSchema` : des colis à livrer passent à un autre livreur (D-219 : sa ville, ou une nouvelle destination).</summary>
public sealed record TransferParcelsCommand : IRequest<object>
{
    [JsonIgnore]
    public bool Async { get; init; }

    [JsonIgnore]
    public bool FromJob { get; init; }

    public IReadOnlyList<string>? ParcelIds { get; init; }
    public string? CourierId { get; init; }
    public string? Reason { get; init => field = Zod.Trim(value); }
    public string? DestinationCityId { get; init; }
}

public sealed class TransferParcelsValidator : ZodValidator<TransferParcelsCommand>
{
    public TransferParcelsValidator()
    {
        RuleFor(x => x.ParcelIds).Required("array").Custom((ids, context) => ParcelTransfers.CheckIds(ids, context.AddFailure));
        RuleFor(x => x.CourierId).Required().MinLength(1, "required");
        RuleFor(x => x.Reason).MaxLength(255);
        RuleFor(x => x.DestinationCityId).Must(v => v is null || v.Length >= 1).WithMessage(Zod.TooSmallString(1));
    }
}

internal sealed class TransferParcelsHandler(IAppDbContext db, ICurrentUser caller, IClock clock, ILogger<TransferParcelsHandler> logger, BulkJobQueue jobs) : IRequestHandler<TransferParcelsCommand, object>
{
    public Task<object> Handle(TransferParcelsCommand input, CancellationToken cancellationToken) =>
        ParcelTransfers.RunAsync(db, caller, clock, logger, jobs, input, new ParcelTransfers.Options(), cancellationToken);
}
