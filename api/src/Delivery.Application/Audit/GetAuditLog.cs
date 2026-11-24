using System.Text.Json;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Audit;

/// <summary>`GET /audit/entry/:id`.</summary>
public sealed record GetAuditLogQuery(string Id) : IRequest<AuditLogDetailDto>;

internal sealed class GetAuditLogHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetAuditLogQuery, AuditLogDetailDto>
{
    public async Task<AuditLogDetailDto> Handle(GetAuditLogQuery request, CancellationToken cancellationToken)
    {
        var row = await AuditRows.Scoped(db.AuditLogs.AsNoTracking(), caller).FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken)
                  ?? throw BusinessException.NotFound("Entrée de journal introuvable.");
        var detail = AuditRows.Fill(new AuditLogDetailDto(), row);
        detail.Before = AuditRows.Json(row.Before);
        detail.After = AuditRows.Json(row.After);
        detail.UserAgent = row.UserAgent;
        detail.RequestId = row.RequestId;
        return detail;
    }
}
