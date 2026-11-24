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

namespace Delivery.Application.Finance;

/// <summary>`codPayoutCreateSchema` : un brouillon pour un vendeur professionnel.</summary>
public sealed record CreateCodPayoutCommand : IRequest<CodPayoutDetailDto>
{
    public string? ClientId { get; init; }
    public string? AgencyId { get; init; }
    public string? Note { get; init => field = Zod.Trim(value); }
}

public sealed class CreateCodPayoutValidator : ZodValidator<CreateCodPayoutCommand>
{
    public CreateCodPayoutValidator()
    {
        RuleFor(x => x.ClientId).Required().MinLength(1, "required");
        RuleFor(x => x.AgencyId).Required().MinLength(1, "required");
        RuleFor(x => x.Note).MaxLength(500);
    }
}

internal sealed class CreateCodPayoutHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<CreateCodPayoutCommand, CodPayoutDetailDto>
{
    public async Task<CodPayoutDetailDto> Handle(CreateCodPayoutCommand input, CancellationToken cancellationToken)
    {
        await ReturnRows.AssertAgencyReachableAsync(db, caller, input.AgencyId!, cancellationToken);
        var client = await db.Clients.AsNoTracking().Where(c => c.Id == input.ClientId && c.DeletedAt == null).Select(c => new { c.Id, c.Name, c.BillingType }).FirstOrDefaultAsync(cancellationToken)
                     ?? throw BusinessException.NotFound("E-commerçant introuvable.");
        if (client.BillingType != BillingType.PROFESSIONAL)
        {
            throw BusinessException.Conflict($"{client.Name} n’est pas un e-commerçant professionnel : son COD est réglé par sa facture.", new { reason = "client-not-professional", clientId = client.Id });
        }
        var (prefix, yearly) = await CodPayouts.NumberingAsync(db, cancellationToken);
        var now = DateTime.UtcNow;
        string code;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            code = await DocumentCodes.ReserveAsync(db, prefix, now, cancellationToken, yearly);
            db.CodPayouts.Add(new CodPayout
            {
                Code = code, ClientId = input.ClientId!, AgencyId = input.AgencyId!, Note = ReturnRows.Text(input.Note), CreatedById = caller.UserId, CreatedAt = now, UpdatedAt = now,
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        return await CodPayouts.DetailAsync(db, caller, code, cancellationToken);
    }
}
