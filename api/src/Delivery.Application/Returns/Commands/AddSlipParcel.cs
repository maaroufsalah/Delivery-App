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

/// <summary>`returnSlipItemSchema` : un colis et son motif.</summary>
public sealed record AddSlipParcelCommand : IRequest<ReturnSlipDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public string? ParcelId { get; init; }
    public string? Reason { get; init; }
}

public sealed class AddSlipParcelValidator : ZodValidator<AddSlipParcelCommand>
{
    public AddSlipParcelValidator()
    {
        RuleFor(x => x.ParcelId).Required().MinLength(1, "required");
        RuleFor(x => x.Reason).OneOf(ReturnRows.Reasons);
    }
}

internal sealed class AddSlipParcelHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<AddSlipParcelCommand, ReturnSlipDetailDto>
{
    public async Task<ReturnSlipDetailDto> Handle(AddSlipParcelCommand input, CancellationToken cancellationToken)
    {
        await Slips.InsertAsync(db, caller, input.Code, input.ParcelId!, Slips.Reason(input.Reason), cancellationToken);
        return await Slips.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}
