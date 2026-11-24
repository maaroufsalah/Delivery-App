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

/// <summary>`remittanceCreateSchema` : un retour de fonds manuel, ouvert avec ses colis.</summary>
public sealed record CreateRemittanceCommand : IRequest<RemittanceDetailDto>
{
    public string? CourierId { get; init; }
    public string? CityId { get; init; }
    public string? AgencyId { get; init; }
    public string? Note { get; init => field = Zod.Trim(value); }
    public IReadOnlyList<string>? ParcelIds { get; init; }
}

public sealed class CreateRemittanceValidator : ZodValidator<CreateRemittanceCommand>
{
    public CreateRemittanceValidator()
    {
        RuleFor(x => x.CourierId).Required().MinLength(1, "required");
        RuleFor(x => x.CityId).Required().MinLength(1, "required");
        RuleFor(x => x.AgencyId).Required().MinLength(1, "required");
        RuleFor(x => x.Note).MaxLength(500);
        RuleFor(x => x.ParcelIds).Custom((ids, context) => RemittanceIds.Check(ids, context, required: false));
    }
}

internal sealed class CreateRemittanceHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<CreateRemittanceCommand, RemittanceDetailDto>
{
    public async Task<RemittanceDetailDto> Handle(CreateRemittanceCommand input, CancellationToken cancellationToken)
    {
        await ReturnRows.AssertAgencyReachableAsync(db, caller, input.AgencyId!, cancellationToken);
        if (!await db.Couriers.AnyAsync(c => c.Id == input.CourierId && c.DeletedAt == null, cancellationToken)) throw BusinessException.NotFound("Livreur introuvable.");
        if (input.ParcelIds is not { Count: > 0 }) throw BusinessException.Validation("Un retour de fonds s’ouvre avec au moins un colis.", new { reason = "remittance-needs-parcels" });
        if (await Remittances.OtherOpenCodeAsync(db, input.CourierId!, cancellationToken) is { } open)
        {
            throw BusinessException.Validation($"Ce livreur a déjà un retour de fonds ouvert : {open}.", new { reason = "courier-has-open-remittance", code = open });
        }
        if (!await db.Cities.AnyAsync(c => c.Id == input.CityId && c.DeletedAt == null, cancellationToken)) throw BusinessException.NotFound("Ville introuvable.");
        var now = DateTime.UtcNow;
        var prefix = await db.SettingAsync(SettingKeys.RemittancePrefix, cancellationToken) is { ValueKind: JsonValueKind.String } p ? p.GetString()! : RemittanceLines.DefaultPrefix;
        var yearly = await db.BoolSettingAsync(SettingKeys.NumberingYearlyReset, false, cancellationToken);
        CashRemittance created;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            var code = await DocumentCodes.ReserveAsync(db, prefix, now, cancellationToken, yearly);
            created = new CashRemittance
            {
                Code = code, CourierId = input.CourierId!, CityId = input.CityId!, AgencyId = input.AgencyId!, Note = ReturnRows.Text(input.Note), CreatedById = caller.UserId, CreatedAt = now,
                UpdatedAt = now,
            };
            db.CashRemittances.Add(created);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                throw BusinessException.Validation("Ce livreur a déjà un retour de fonds ouvert.", new { reason = "courier-has-open-remittance" });
            }
            await transaction.CommitAsync(cancellationToken);
        }
        db.ChangeTracker.Clear();
        try
        {
            await Remittances.AddParcelsAsync(db, caller, created.Code, input.ParcelIds, cancellationToken);
        }
        catch (BusinessException)
        {
            db.ChangeTracker.Clear();
            await db.CashRemittances.Where(r => r.Id == created.Id).ExecuteDeleteAsync(cancellationToken);
            throw;
        }
        return await Remittances.DetailAsync(db, caller, created.Code, cancellationToken);
    }
}
