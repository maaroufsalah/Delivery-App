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

/// <summary>`GET /petty-cash`.</summary>
public sealed record GetPettyCashQuery(PettyCashFilter Filter) : IRequest<PettyCashPageDto>;

internal sealed class GetPettyCashHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<GetPettyCashQuery, PettyCashPageDto>
{
    public Task<PettyCashPageDto> Handle(GetPettyCashQuery request, CancellationToken cancellationToken) => PettyCashMovements.ListAsync(db, caller, clock, request.Filter, cancellationToken);
}
