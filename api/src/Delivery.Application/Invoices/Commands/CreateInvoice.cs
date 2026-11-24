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

/// <summary>`invoiceCreateSchema` : un brouillon vide ; un seul brouillon ouvert par vendeur.</summary>
public sealed record CreateInvoiceCommand : IRequest<InvoiceDetailDto>
{
    [JsonIgnore]
    public string? Kind { get; init; }

    public string? ClientId { get; init; }
    public string? AgencyId { get; init; }
    public string? Note { get; init => field = Zod.Trim(value); }
}

public sealed class CreateInvoiceValidator : ZodValidator<CreateInvoiceCommand>
{
    public CreateInvoiceValidator()
    {
        RuleFor(x => x.ClientId).Required().MinLength(1, "required");
        RuleFor(x => x.AgencyId).Required().MinLength(1, "required");
        RuleFor(x => x.Note).MaxLength(500);
    }
}

internal sealed class CreateInvoiceHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<CreateInvoiceCommand, InvoiceDetailDto>
{
    public async Task<InvoiceDetailDto> Handle(CreateInvoiceCommand input, CancellationToken cancellationToken)
    {
        await ReturnRows.AssertAgencyReachableAsync(db, caller, input.AgencyId!, cancellationToken);
        var client = await db.Clients.AsNoTracking().Where(c => c.Id == input.ClientId && c.DeletedAt == null).Select(c => new { c.Id, c.Name, c.BillingType, c.IsPro }).FirstOrDefaultAsync(cancellationToken)
                     ?? throw BusinessException.NotFound("E-commerçant introuvable.");
        InvoiceRows.AssertKind(client.Name, client.IsPro, input.Kind);
        var now = DateTime.UtcNow;
        var (prefix, yearly) = await InvoiceDrafts.NumberingAsync(db, cancellationToken);
        string code;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            if (await InvoiceDrafts.OpenDraftOfAsync(db, client.Id, cancellationToken) is { } draft)
            {
                throw BusinessException.Conflict($"{client.Name} a déjà un brouillon ouvert : {draft.Code}. Ajoutez-y les colis plutôt que d’en ouvrir un second.",
                    new { reason = "draft-exists", invoiceCode = draft.Code });
            }
            code = await DocumentCodes.ReserveAsync(db, prefix, now, cancellationToken, yearly);
            db.Invoices.Add(new Invoice
            {
                Code = code, ClientId = client.Id, AgencyId = input.AgencyId!, BillingType = client.BillingType, Note = ReturnRows.Text(input.Note), CreatedById = caller.UserId, CreatedAt = now,
                UpdatedAt = now,
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        return await InvoiceRows.DetailAsync(db, caller, code, cancellationToken);
    }
}
