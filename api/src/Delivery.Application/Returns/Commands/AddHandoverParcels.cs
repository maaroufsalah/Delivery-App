using System.Globalization;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.Routes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Returns;

/// <summary>`returnHandoverParcelsSchema` : des colis cochés, ou « tout ».</summary>
public sealed record AddHandoverParcelsCommand : ParcelSelection, IRequest<ReturnHandoverDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public string? Note { get; init => field = Zod.Trim(value); }
}

public sealed class AddHandoverParcelsValidator : ParcelSelectionValidator<AddHandoverParcelsCommand>
{
    public AddHandoverParcelsValidator()
    {
        RuleFor(x => x.Note).MaxLength(255);
    }
}

internal sealed class AddHandoverParcelsHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<AddHandoverParcelsCommand, ReturnHandoverDetailDto>
{
    public Task<ReturnHandoverDetailDto> Handle(AddHandoverParcelsCommand input, CancellationToken cancellationToken) =>
        Handovers.AddParcelsAsync(db, caller, input.Code, input.ParcelIds, input.All, input.Exclude, input.Search, input.Note, cancellationToken);
}
