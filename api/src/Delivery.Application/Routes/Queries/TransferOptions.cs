using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Delivery.Application.BulkJobs;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels.Commands;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Routes;

/// <summary>`parcelTransferOptionsSchema` : ce que la modale propose (villes du livreur, destination gardée).</summary>
public sealed record TransferOptionsQuery : IRequest<ParcelTransferOptionsDto>
{
    public IReadOnlyList<string>? ParcelIds { get; init; }
    public string? CourierId { get; init; }
}

public sealed class TransferOptionsValidator : ZodValidator<TransferOptionsQuery>
{
    public TransferOptionsValidator()
    {
        RuleFor(x => x.ParcelIds).Required("array").Custom((ids, context) => ParcelTransfers.CheckIds(ids, context.AddFailure));
        RuleFor(x => x.CourierId).Required().MinLength(1, "required");
    }
}

internal sealed class TransferOptionsHandler(IAppDbContext db) : IRequestHandler<TransferOptionsQuery, ParcelTransferOptionsDto>
{
    public async Task<ParcelTransferOptionsDto> Handle(TransferOptionsQuery input, CancellationToken cancellationToken)
    {
        var ids = input.ParcelIds!.Distinct().ToList();
        var courier = await ParcelTransfers.CourierAsync(db, input.CourierId!, cancellationToken);
        var parcels = await db.Parcels.AsNoTracking().Where(p => ids.Contains(p.Id) && p.DeletedAt == null)
            .Select(p => new TransferCityDto(p.DestinationCityId, p.DestinationCity.Name)).ToListAsync(cancellationToken);
        if (parcels.Count != ids.Count) throw BusinessException.Validation("Colis inconnu.", new { field = "parcelIds" });
        var served = courier.Cities.Select(c => c.Id).Append(courier.CityId).ToHashSet();
        var toReroute = parcels.Count(p => !served.Contains(p.Id));
        if (courier.Cities.Count == 0) return new ParcelTransferOptionsDto([], null, toReroute, true, ids.Count);
        var distinct = parcels.DistinctBy(p => p.Id).ToList();
        var keep = distinct.Count == 1 && served.Contains(distinct[0].Id) ? distinct[0] : null;
        var cities = courier.Cities.ToList();
        if (keep is not null && cities.All(c => c.Id != keep.Id)) cities.Add(keep);
        return new ParcelTransferOptionsDto(cities.OrderBy(c => c.Name, StringComparer.Create(CultureInfo.GetCultureInfo("fr"), false)).ToList(), keep?.Id, toReroute, false, ids.Count);
    }
}
