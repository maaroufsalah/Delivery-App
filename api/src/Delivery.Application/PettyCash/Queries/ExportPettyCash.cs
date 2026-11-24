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

/// <summary>`GET /petty-cash/export` : les mouvements validés du filtre, classeur « Petite caisse ».</summary>
public sealed record ExportPettyCashQuery(PettyCashFilter Filter) : IRequest<FileContent>;

internal sealed class ExportPettyCashHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IExcelService excel) : IRequestHandler<ExportPettyCashQuery, FileContent>
{
    public async Task<FileContent> Handle(ExportPettyCashQuery request, CancellationToken cancellationToken)
    {
        var (rows, _) = await PettyCashMovements.ValidatedAsync(db, caller, clock, request.Filter, Exports.MaxRows, cancellationToken);
        return await Exports.ToXlsxAsync(db, excel, clock, caller, "Petite caisse", PettyCashExports.Columns, rows, cancellationToken);
    }
}
