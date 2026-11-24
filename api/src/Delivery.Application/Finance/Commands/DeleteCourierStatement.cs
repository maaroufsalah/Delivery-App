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

/// <summary>`DELETE /courier-statements/:code` : un brouillon seulement.</summary>
public sealed record DeleteCourierStatementCommand(string Code) : IRequest<ReturnCodeDto>;

internal sealed class DeleteCourierStatementHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<DeleteCourierStatementCommand, ReturnCodeDto>
{
    public async Task<ReturnCodeDto> Handle(DeleteCourierStatementCommand request, CancellationToken cancellationToken)
    {
        var statement = await CourierStatements.Scoped(db.CourierStatements.AsNoTracking(), caller).Where(s => s.Code == request.Code).Select(s => new { s.Id, s.Status })
                            .FirstOrDefaultAsync(cancellationToken) ?? throw BusinessException.NotFound($"Relevé introuvable : {request.Code}");
        if (statement.Status != CourierStatementStatus.DRAFT) throw BusinessException.Conflict("Un relevé validé ne se supprime pas.", new { reason = "statement-validated" });
        await db.CourierStatements.Where(s => s.Id == statement.Id).ExecuteDeleteAsync(cancellationToken);
        return new ReturnCodeDto(request.Code);
    }
}
