using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.CourierApp;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Application.Routes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using Delivery.Domain.Pricing;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Returns;

/// <summary>`GET /return-slips/:code`.</summary>
public sealed record GetReturnSlipQuery(string Code) : IRequest<ReturnSlipDetailDto>;

internal sealed class GetReturnSlipHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetReturnSlipQuery, ReturnSlipDetailDto>
{
    public Task<ReturnSlipDetailDto> Handle(GetReturnSlipQuery request, CancellationToken cancellationToken) => Slips.DetailAsync(db, caller, request.Code, cancellationToken);
}
