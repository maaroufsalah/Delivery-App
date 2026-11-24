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

namespace Delivery.Application.Finance;

/// <summary>`courierStatementSchema` : le relevé d'un livreur sur une période, ses courses calculées.</summary>
public sealed record CreateCourierStatementCommand : IRequest<CourierStatementDetailDto>
{
    public string? CourierId { get; init; }
    public string? AgencyId { get; init; }
    public string? PeriodStart { get; init => field = Zod.Trim(value); }
    public string? PeriodEnd { get; init => field = Zod.Trim(value); }
    public string? Note { get; init => field = Zod.Trim(value); }
}

public sealed class CreateCourierStatementValidator : ZodValidator<CreateCourierStatementCommand>
{
    public CreateCourierStatementValidator()
    {
        RuleFor(x => x.CourierId).Required().MinLength(1, "required");
        RuleFor(x => x.AgencyId).Required().MinLength(1, "required");
        RuleFor(x => x.PeriodStart).Required().Matches(@"^\d{4}-\d{2}-\d{2}$").WithMessage("invalid-date");
        RuleFor(x => x.PeriodEnd).Required().Matches(@"^\d{4}-\d{2}-\d{2}$").WithMessage("invalid-date");
        RuleFor(x => x.Note).MaxLength(500);
    }

    protected override void Refine(CreateCourierStatementCommand value, Action<string, string> issue)
    {
        if (value.PeriodStart is { } a && value.PeriodEnd is { } b && string.CompareOrdinal(a, b) > 0) issue(ValidationPaths.Raw + "periodEnd", "period-reversed");
    }
}

internal sealed class CreateCourierStatementHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<CreateCourierStatementCommand, CourierStatementDetailDto>
{
    public async Task<CourierStatementDetailDto> Handle(CreateCourierStatementCommand input, CancellationToken cancellationToken)
    {
        await ReturnRows.AssertAgencyReachableAsync(db, caller, input.AgencyId!, cancellationToken);
        var courier = await db.Couriers.AsNoTracking().Where(c => c.Id == input.CourierId && c.DeletedAt == null)
                          .Select(c => new { c.Id, c.PayModel, c.PayPeriod, c.FixedSalary, c.SettlementMode }).FirstOrDefaultAsync(cancellationToken)
                      ?? throw BusinessException.NotFound("Livreur introuvable.");
        var start = StatementLists.Day(input.PeriodStart!);
        var end = StatementLists.Day(input.PeriodEnd!).AddDays(1).AddMilliseconds(-1);
        var lines = await CourierStatements.ComputeLinesAsync(db, courier.Id, start, end, cancellationToken);
        var cod = await CourierStatements.CodCollectedAsync(db, courier.Id, start, end, cancellationToken);
        var totals = CourierStatements.TotalsOf(courier.PayModel, Money.ToCents(courier.FixedSalary ?? 0m), lines);
        var prefix = await db.SettingAsync(SettingKeys.StatementPrefix, cancellationToken) is { ValueKind: JsonValueKind.String } p ? p.GetString()! : CourierStatements.DefaultPrefix;
        var yearly = await db.BoolSettingAsync(SettingKeys.NumberingYearlyReset, false, cancellationToken);
        var now = DateTime.UtcNow;
        string code;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            code = await DocumentCodes.ReserveAsync(db, prefix, now, cancellationToken, yearly);
            var statement = new CourierStatement
            {
                Code = code, CourierId = courier.Id, AgencyId = input.AgencyId!, PeriodStart = start, PeriodEnd = end, PeriodKind = courier.PayPeriod, PayModel = courier.PayModel,
                SettlementMode = courier.SettlementMode, BaseAmount = Money.FromCents(totals.Base), CodCollected = Money.FromCents(cod), CommissionsTotal = Money.FromCents(totals.CommissionsTotal),
                CommissionsDeducted = Money.FromCents(totals.CommissionsDeducted), VariableAmount = Money.FromCents(totals.Variable), TotalDue = Money.FromCents(totals.Due),
                Note = ReturnRows.Text(input.Note), CreatedById = caller.UserId, CreatedAt = now, UpdatedAt = now,
            };
            db.CourierStatements.Add(statement);
            foreach (var line in lines) db.CourierStatementLines.Add(CourierStatements.LineOf(statement.Id, line));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        return await CourierStatements.DetailAsync(db, caller, code, cancellationToken);
    }
}
