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

/// <summary>`GET /petty-cash/days/:day`.</summary>
public sealed record GetPettyCashDayQuery(string Day, string? AgencyId) : IRequest<PettyCashDayDetailDto>;

internal sealed class GetPettyCashDayHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetPettyCashDayQuery, PettyCashDayDetailDto>
{
    public Task<PettyCashDayDetailDto> Handle(GetPettyCashDayQuery request, CancellationToken cancellationToken)
    {
        DayParam.Check(request.Day);
        return PettyCashDays.DetailAsync(db, caller, request.Day, request.AgencyId, cancellationToken);
    }
}
