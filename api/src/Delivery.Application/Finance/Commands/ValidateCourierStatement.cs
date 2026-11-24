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

/// <summary>`POST /courier-statements/:code/validate` : chaque course est marquée payée ; une course ne se paie qu'une fois.</summary>
public sealed record ValidateCourierStatementCommand(string Code) : IRequest<CourierStatementDetailDto>;

internal sealed class ValidateCourierStatementHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<ValidateCourierStatementCommand, CourierStatementDetailDto>
{
    public async Task<CourierStatementDetailDto> Handle(ValidateCourierStatementCommand request, CancellationToken cancellationToken)
    {
        var statement = await CourierStatements.Scoped(db.CourierStatements.AsNoTracking(), caller).Where(s => s.Code == request.Code)
                            .Select(s => new { s.Id, s.Status, s.AgencyId, Lines = s.Lines.Select(l => new { l.Id, l.ParcelId }).ToList() }).FirstOrDefaultAsync(cancellationToken)
                        ?? throw BusinessException.NotFound($"Relevé introuvable : {request.Code}");
        if (statement.Status != CourierStatementStatus.DRAFT) throw BusinessException.Conflict("Ce relevé est déjà validé.", new { reason = "statement-already-validated" });
        await ReturnRows.AssertAgencyReachableAsync(db, caller, statement.AgencyId, cancellationToken);
        var now = DateTime.UtcNow;
        var userId = caller.UserId;
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            foreach (var line in statement.Lines)
            {
                await db.CourierStatementLines.Where(l => l.Id == line.Id).ExecuteUpdateAsync(set => set.SetProperty(l => l.SettledParcelId, line.ParcelId), cancellationToken);
            }
            await db.CourierStatements.Where(s => s.Id == statement.Id).ExecuteUpdateAsync(set => set.SetProperty(s => s.Status, CourierStatementStatus.VALIDATED).SetProperty(s => s.ValidatedAt, now)
                .SetProperty(s => s.ValidatedById, userId).SetProperty(s => s.UpdatedAt, now), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Npgsql.PostgresException error) when (error.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation)
        {
            throw BusinessException.Conflict("Une course de ce relevé vient d’être payée ailleurs. Recalculez le brouillon.", new { reason = "course-already-settled" });
        }
        return await CourierStatements.DetailAsync(db, caller, request.Code, cancellationToken);
    }
}
