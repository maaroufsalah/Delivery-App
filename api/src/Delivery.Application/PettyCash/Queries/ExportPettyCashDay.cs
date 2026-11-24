using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Idempotency;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.PettyCash;

/// <summary>`GET /petty-cash/days/:day/export` : la journée en classeur.</summary>
public sealed record ExportPettyCashDayQuery(string Day, string? AgencyId) : IRequest<FileContent>;

internal sealed class ExportPettyCashDayHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IExcelService excel) : IRequestHandler<ExportPettyCashDayQuery, FileContent>
{
    public async Task<FileContent> Handle(ExportPettyCashDayQuery request, CancellationToken cancellationToken)
    {
        DayParam.Check(request.Day);
        await PettyCashDays.DetailAsync(db, caller, request.Day, request.AgencyId, cancellationToken);
        var (rows, _) = await PettyCashMovements.ValidatedAsync(db, caller, clock, PettyCashExports.Day(request.Day, request.AgencyId), Exports.MaxRows, cancellationToken);
        return await Exports.ToXlsxAsync(db, excel, clock, caller, $"Petite caisse {request.Day}", PettyCashExports.Columns, rows, cancellationToken);
    }
}
