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

/// <summary>`POST /courier-statements/:code/recompute`.</summary>
public sealed record RecomputeCourierStatementCommand(string Code) : IRequest<CourierStatementDetailDto>;

internal sealed class RecomputeCourierStatementHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<RecomputeCourierStatementCommand, CourierStatementDetailDto>
{
    public async Task<CourierStatementDetailDto> Handle(RecomputeCourierStatementCommand request, CancellationToken cancellationToken)
    {
        await CourierStatements.RecomputeAsync(db, caller, request.Code, cancellationToken);
        return await CourierStatements.DetailAsync(db, caller, request.Code, cancellationToken);
    }
}
