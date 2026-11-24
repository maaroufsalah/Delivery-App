using System.Text.Json;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.Returns;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Finance;

/// <summary>`remittanceParcelsSchema`.</summary>
public sealed record AddRemittanceParcelsCommand : IRequest<RemittanceDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public IReadOnlyList<string>? ParcelIds { get; init; }
}

public sealed class AddRemittanceParcelsValidator : ZodValidator<AddRemittanceParcelsCommand>
{
    public AddRemittanceParcelsValidator()
    {
        RuleFor(x => x.ParcelIds).Required("array").Custom((ids, context) => RemittanceIds.Check(ids, context, required: true));
    }
}

internal sealed class AddRemittanceParcelsHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<AddRemittanceParcelsCommand, RemittanceDetailDto>
{
    public async Task<RemittanceDetailDto> Handle(AddRemittanceParcelsCommand input, CancellationToken cancellationToken)
    {
        await Remittances.AddParcelsAsync(db, caller, input.Code, input.ParcelIds!, cancellationToken);
        return await Remittances.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}
