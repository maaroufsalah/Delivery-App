using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Bl;

/// <summary>`GET /bl/parcels/:code`.</summary>
public sealed record GetParcelBlQuery(string Code) : IRequest<ParcelBlTrackingDto>;

internal sealed class GetParcelBlHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetParcelBlQuery, ParcelBlTrackingDto>
{
    public Task<ParcelBlTrackingDto> Handle(GetParcelBlQuery request, CancellationToken cancellationToken) => BlRows.TrackingAsync(db, caller, request.Code, cancellationToken);
}
