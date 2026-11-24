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

/// <summary>`GET /courier-statements/:code`.</summary>
public sealed record GetCourierStatementQuery(string Code) : IRequest<CourierStatementDetailDto>;

internal sealed class GetCourierStatementHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetCourierStatementQuery, CourierStatementDetailDto>
{
    public Task<CourierStatementDetailDto> Handle(GetCourierStatementQuery request, CancellationToken cancellationToken) => CourierStatements.DetailAsync(db, caller, request.Code, cancellationToken);
}
