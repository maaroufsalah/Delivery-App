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
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Application.PettyCash;
using Delivery.Application.Returns;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Finance;

/// <summary>`GET /cod-payouts/:code`.</summary>
public sealed record GetCodPayoutQuery(string Code) : IRequest<CodPayoutDetailDto>;

internal sealed class GetCodPayoutHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetCodPayoutQuery, CodPayoutDetailDto>
{
    public Task<CodPayoutDetailDto> Handle(GetCodPayoutQuery request, CancellationToken cancellationToken) => CodPayouts.DetailAsync(db, caller, request.Code, cancellationToken);
}
