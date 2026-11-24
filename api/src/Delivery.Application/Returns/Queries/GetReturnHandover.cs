using System.Globalization;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.Routes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Returns;

/// <summary>`GET /return-handovers/:code`.</summary>
public sealed record GetReturnHandoverQuery(string Code) : IRequest<ReturnHandoverDetailDto>;

internal sealed class GetReturnHandoverHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetReturnHandoverQuery, ReturnHandoverDetailDto>
{
    public Task<ReturnHandoverDetailDto> Handle(GetReturnHandoverQuery request, CancellationToken cancellationToken) => Handovers.DetailAsync(db, caller, request.Code, cancellationToken);
}
