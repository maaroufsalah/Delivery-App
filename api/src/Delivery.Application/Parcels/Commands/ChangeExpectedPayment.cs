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

/// <summary>`PATCH /parcels/:code/expected-payment` : espèces ou chèque (colis Pro), motif journalisé.</summary>
public sealed record ChangeExpectedPaymentCommand : IRequest<ParcelDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public string? ExpectedPayment { get; init; }

    public string? Reason { get; init => field = Zod.Trim(value); }
}

public sealed class ChangeExpectedPaymentValidator : ZodValidator<ChangeExpectedPaymentCommand>
{
    public ChangeExpectedPaymentValidator()
    {
        RuleFor(x => x.ExpectedPayment).Required().OneOf(ParcelInputs.ExpectedPayments);
        RuleFor(x => x.Reason).Required().MinLength(1, "required").MaxLength(255);
    }
}

internal sealed class ChangeExpectedPaymentHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<ChangeExpectedPaymentCommand, ParcelDetailDto>
{
    public async Task<ParcelDetailDto> Handle(ChangeExpectedPaymentCommand input, CancellationToken cancellationToken)
    {
        var parcel = await ParcelFilters.Scoped(db.Parcels, caller).Include(p => p.Client).FirstOrDefaultAsync(p => p.Code == input.Code, cancellationToken)
                     ?? throw BusinessException.NotFound($"Colis introuvable : {input.Code}");
        if (!parcel.Client.IsPro && parcel.ExpectedPayment is null)
        {
            throw BusinessException.Validation("Le paiement attendu ne concerne que les colis d’un Client Pro.", new { reason = "client-not-pro" });
        }
        var next = input.ExpectedPayment == "cheque" ? ExpectedPayment.CHEQUE : ExpectedPayment.CASH;
        if (parcel.ExpectedPayment == next) throw BusinessException.Validation("Le paiement attendu est déjà à cette valeur.");
        var author = await ParcelStatusChange.AuthorNameAsync(db, caller.UserId, cancellationToken);
        var old = parcel.ExpectedPayment is { } current ? Money.ToWire(current) : null;
        parcel.ExpectedPayment = next;
        ParcelJournal.Record(db, caller, parcel.Id, "expectedPayment", old, input.ExpectedPayment!, input.Reason, author);
        await db.SaveChangesAsync(cancellationToken);
        return await GetParcelHandler.LoadAsync(db, caller, mapper, new GetParcelQuery(input.Code), cancellationToken);
    }
}
