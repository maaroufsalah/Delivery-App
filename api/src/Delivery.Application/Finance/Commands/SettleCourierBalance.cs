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

/// <summary>`courierSettlementSchema` : le règlement d'un écart, strictement positif, en centimes.</summary>
public sealed record SettleCourierBalanceCommand : IRequest<CourierBalanceDto>
{
    [JsonIgnore]
    public string CourierId { get; init; } = string.Empty;

    public JsonElement? Amount { get; init; }
    public string? Method { get; init; }
    public string? Note { get; init => field = Zod.Trim(value); }
}

public sealed class SettleCourierBalanceValidator : ZodValidator<SettleCourierBalanceCommand>
{
    public SettleCourierBalanceValidator()
    {
        RuleFor(x => x.Amount).Custom((amount, context) =>
        {
            if (amount is not { ValueKind: JsonValueKind.Number } value) { context.AddFailure(ValidationPaths.Raw + "amount", Zod.Expected("number", Zod.Received(amount))); return; }
            var number = value.GetDouble();
            if (number != Math.Floor(number)) context.AddFailure(ValidationPaths.Raw + "amount", Zod.ExpectedInt);
            else if (number <= 0) context.AddFailure(ValidationPaths.Raw + "amount", "Too small: expected number to be >0");
        });
        RuleFor(x => x.Method).OneOf(["cash", "transfer", "check"]);
        RuleFor(x => x.Note).MaxLength(500);
    }
}

internal sealed class SettleCourierBalanceHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<SettleCourierBalanceCommand, CourierBalanceDto>
{
    public async Task<CourierBalanceDto> Handle(SettleCourierBalanceCommand input, CancellationToken cancellationToken)
    {
        if (caller.CourierId is not null) throw BusinessException.Forbidden("Seule l’agence enregistre un règlement d’écart.", new { reason = "settlement-is-agency-only" });
        if (!await db.Couriers.AnyAsync(c => c.Id == input.CourierId, cancellationToken)) throw BusinessException.NotFound("Livreur introuvable.");
        db.CourierBalanceEntries.Add(new CourierBalanceEntry
        {
            CourierId = input.CourierId, Kind = CourierBalanceEntryKind.SETTLEMENT, Amount = Money.FromCents(input.Amount!.Value.GetInt64()),
            Method = Enum.Parse<PaymentMethod>((input.Method ?? "cash").ToUpperInvariant()), Note = ReturnRows.Text(input.Note), AuthorId = caller.UserId,
            AuthorName = await Remittances.AuthorNameAsync(db, caller.UserId, cancellationToken), At = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
        return await Remittances.BalanceAsync(db, input.CourierId, cancellationToken);
    }
}
