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

/// <summary>`PATCH /parcels/:code/fees` : COD et frais corrigés, motif obligatoire, journalisés.</summary>
public sealed record EditParcelFeesCommand : IRequest<ParcelDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public JsonElement? CodAmount { get; init; }

    public JsonElement? DeliveryFee { get; init; }

    public JsonElement? FailureFee { get; init; }

    public JsonElement? OtherFees { get; init; }

    public string? Reason { get; init => field = Zod.Trim(value); }
}

public sealed class EditParcelFeesValidator : ZodValidator<EditParcelFeesCommand>
{
    public EditParcelFeesValidator()
    {
        Amount(x => x.CodAmount, "codAmount");
        Amount(x => x.DeliveryFee, "deliveryFee");
        Amount(x => x.FailureFee, "failureFee");
        Amount(x => x.OtherFees, "otherFees");
        RuleFor(x => x.Reason).Required().MinLength(1, "required").MaxLength(500);
    }

    private void Amount(System.Linq.Expressions.Expression<Func<EditParcelFeesCommand, JsonElement?>> field, string path) =>
        RuleFor(field).Custom((value, context) =>
        {
            if (OptionalMad.Issue(value) is { } issue) context.AddFailure(ValidationPaths.Raw + path, issue);
        });

    protected override void Refine(EditParcelFeesCommand value, Action<string, string> issue)
    {
        if (!OptionalMad.IsPresent(value.CodAmount) && !OptionalMad.IsPresent(value.DeliveryFee) && !OptionalMad.IsPresent(value.FailureFee) && !OptionalMad.IsPresent(value.OtherFees))
        {
            issue(ValidationPaths.Raw + "deliveryFee", "no-fee-changed");
        }
    }
}

internal sealed class EditParcelFeesHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<EditParcelFeesCommand, ParcelDetailDto>
{
    public async Task<ParcelDetailDto> Handle(EditParcelFeesCommand input, CancellationToken cancellationToken)
    {
        var parcel = await ParcelFilters.Scoped(db.Parcels, caller).FirstOrDefaultAsync(p => p.Code == input.Code, cancellationToken)
                     ?? throw BusinessException.NotFound($"Colis introuvable : {input.Code}");
        var settled = await db.InvoiceLines.AsNoTracking()
            .Where(l => l.ParcelId == parcel.Id && l.InvoicedParcelId != null && l.Invoice.Status == InvoiceStatus.PAID)
            .Select(l => l.Invoice.Code).FirstOrDefaultAsync(cancellationToken);
        if (settled is not null)
        {
            throw BusinessException.Validation($"Le colis {parcel.Code} est sur la facture {settled}, déjà réglée : ses montants ne se modifient plus.",
                new { reason = "parcel-invoice-paid", code = parcel.Code, invoiceCode = settled });
        }
        var cod = OptionalMad.Value(input.CodAmount);
        if (cod is not null && Money.MadToCents(cod.Value) != Money.ToCents(parcel.CodAmount))
        {
            var remitted = await db.CashRemittanceParcels.AsNoTracking().Where(r => r.ParcelId == parcel.Id && r.ConfirmedParcelId == parcel.Id)
                .Select(r => r.Remittance.Code).FirstOrDefaultAsync(cancellationToken);
            if (remitted is not null)
            {
                throw BusinessException.Validation($"Le cash du colis {parcel.Code} a été remis ({remitted}) : son COD ne se modifie plus.",
                    new { reason = "parcel-cod-remitted", code = parcel.Code, remittanceCode = remitted });
            }
        }
        var author = await ParcelStatusChange.AuthorNameAsync(db, caller.UserId, cancellationToken);
        var changes = new List<(string Field, long? Old, long New)>();
        void Bump(string field, double? next, long? current, Action<decimal> apply)
        {
            if (next is null) return;
            var cents = Money.MadToCents(next.Value);
            if (current == cents) return;
            changes.Add((field, current, cents));
            apply(Money.FromCents(cents));
        }
        Bump("codAmount", cod, Money.ToCents(parcel.CodAmount), v => parcel.CodAmount = v);
        Bump("deliveryFee", OptionalMad.Value(input.DeliveryFee), Money.ToCents(parcel.DeliveryFee), v => parcel.DeliveryFee = v);
        Bump("failureFee", OptionalMad.Value(input.FailureFee), Money.ToCentsOrNull(parcel.FailureFee), v => parcel.FailureFee = v);
        Bump("otherFees", OptionalMad.Value(input.OtherFees), Money.ToCents(parcel.OtherFees), v => parcel.OtherFees = v);
        if (changes.Count == 0) throw BusinessException.Validation("Les montants sont déjà à ces valeurs.");
        foreach (var (field, old, next) in changes)
        {
            ParcelJournal.Record(db, caller, parcel.Id, field, old?.ToString(CultureInfo.InvariantCulture), next.ToString(CultureInfo.InvariantCulture), input.Reason, author);
        }
        await db.SaveChangesAsync(cancellationToken);
        return await GetParcelHandler.LoadAsync(db, caller, mapper, new GetParcelQuery(input.Code), cancellationToken);
    }
}
