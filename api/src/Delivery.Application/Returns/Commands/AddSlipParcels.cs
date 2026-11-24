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

/// <summary>`returnSlipItemsSchema` : des colis cochés, ou « tout », avec un même motif.</summary>
public sealed record AddSlipParcelsCommand : ParcelSelection, IRequest<ReturnSlipDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public string? Reason { get; init; }
}

public sealed class AddSlipParcelsValidator : ParcelSelectionValidator<AddSlipParcelsCommand>
{
    public AddSlipParcelsValidator()
    {
        RuleFor(x => x.Reason).OneOf(ReturnRows.Reasons);
    }
}

internal sealed class AddSlipParcelsHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<AddSlipParcelsCommand, ReturnSlipDetailDto>
{
    public async Task<ReturnSlipDetailDto> Handle(AddSlipParcelsCommand input, CancellationToken cancellationToken)
    {
        var ids = await NoteRules.ResolveSelectionAsync(input.ParcelIds, input.All, input.Exclude,
            async _ => (await Slips.CandidatesAsync(db, caller, input.Code, input.Search, cancellationToken)).Select(l => l.Id).ToList());
        var reason = Slips.Reason(input.Reason);
        foreach (var id in ids) await Slips.InsertAsync(db, caller, input.Code, id, reason, cancellationToken);
        return await Slips.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}
