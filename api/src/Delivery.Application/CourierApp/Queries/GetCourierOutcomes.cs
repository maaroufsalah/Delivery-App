using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Idempotency;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Application.Routes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using Delivery.Domain.Parcels;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.CourierApp;

/// <summary>`GET /courier/parcels/:code/outcomes` : les issues que le livreur peut poser.</summary>
public sealed record GetCourierOutcomesQuery(string Code) : IRequest<IReadOnlyList<int>>;

internal sealed class GetCourierOutcomesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetCourierOutcomesQuery, IReadOnlyList<int>>
{
    public Task<IReadOnlyList<int>> Handle(GetCourierOutcomesQuery request, CancellationToken cancellationToken) => CourierParcels.OutcomesAsync(db, caller, request.Code, cancellationToken);
}
