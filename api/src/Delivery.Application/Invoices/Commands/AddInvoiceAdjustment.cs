using System.Globalization;
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
using Delivery.Application.Finance;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.PettyCash;
using Delivery.Application.Returns;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Invoices;

/// <summary>`invoiceAdjustmentSchema` : une ligne hors colis, signée, avec son motif.</summary>
public sealed record AddInvoiceAdjustmentCommand : IRequest<InvoiceDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public string? Kind { get; init; }
    public JsonElement? Amount { get; init; }
    public string? Reason { get; init => field = Zod.Trim(value); }
    public string? Reference { get; init => field = Zod.Trim(value); }
}

public sealed class AddInvoiceAdjustmentValidator : ZodValidator<AddInvoiceAdjustmentCommand>
{
    public AddInvoiceAdjustmentValidator()
    {
        RuleFor(x => x.Kind).OneOf(["adjustment", "other"]);
        RuleFor(x => x.Amount).Custom((amount, context) =>
        {
            if (amount is not { ValueKind: JsonValueKind.Number } value) { context.AddFailure(ValidationPaths.Raw + "amount", Zod.Expected("number", Zod.Received(amount))); return; }
            var number = value.GetDouble();
            if (number == 0) context.AddFailure(ValidationPaths.Raw + "amount", "required");
            else if (Math.Abs(number) > 10_000_000) context.AddFailure(ValidationPaths.Raw + "amount", "too-large");
        });
        RuleFor(x => x.Reason).Required().MinLength(1, "required").MaxLength(300, "too-long");
        RuleFor(x => x.Reference).MaxLength(80);
    }
}

internal sealed class AddInvoiceAdjustmentHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<AddInvoiceAdjustmentCommand, InvoiceDetailDto>
{
    public async Task<InvoiceDetailDto> Handle(AddInvoiceAdjustmentCommand input, CancellationToken cancellationToken)
    {
        var invoice = await InvoiceRows.RequireDraftAsync(db, caller, input.Code, cancellationToken);
        var authorName = await InvoiceRows.AuthorNameAsync(db, caller.UserId, cancellationToken);
        var amount = (long)Math.Round(input.Amount!.Value.GetDouble() * 100, MidpointRounding.AwayFromZero);
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await InvoiceDrafts.WriteAdjustmentAsync(db, caller, invoice.Id, input.Kind == "other" ? InvoiceAdjustmentKind.OTHER : InvoiceAdjustmentKind.ADJUSTMENT, amount, input.Reason!,
                ReturnRows.Text(input.Reference), null, authorName, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        db.ChangeTracker.Clear();
        return await InvoiceRows.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}
