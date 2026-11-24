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

/// <summary>`returnSlipSchema` : un brouillon de bon de retour pour un e-commerçant.</summary>
public sealed record CreateReturnSlipCommand : IRequest<ReturnSlipDetailDto>
{
    public string? ClientId { get; init; }
    public string? AgencyId { get; init; }
    public string? Note { get; init => field = Zod.Trim(value); }
}

public sealed class CreateReturnSlipValidator : ZodValidator<CreateReturnSlipCommand>
{
    public CreateReturnSlipValidator()
    {
        RuleFor(x => x.ClientId).Required().MinLength(1, "required");
        RuleFor(x => x.AgencyId).Required().MinLength(1, "required");
        RuleFor(x => x.Note).MaxLength(500);
    }
}

internal sealed class CreateReturnSlipHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<CreateReturnSlipCommand, ReturnSlipDetailDto>
{
    public async Task<ReturnSlipDetailDto> Handle(CreateReturnSlipCommand input, CancellationToken cancellationToken)
    {
        await ReturnRows.AssertAgencyReachableAsync(db, caller, input.AgencyId!, cancellationToken);
        if (!await db.Clients.AnyAsync(c => c.Id == input.ClientId && c.DeletedAt == null, cancellationToken)) throw BusinessException.NotFound("E-commerçant introuvable.");
        var now = DateTime.UtcNow;
        ReturnSlip slip;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            slip = await Slips.OpenAsync(db, caller, input.ClientId!, input.AgencyId!, input.Note, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        return await Slips.DetailAsync(db, caller, slip.Code, cancellationToken);
    }
}
