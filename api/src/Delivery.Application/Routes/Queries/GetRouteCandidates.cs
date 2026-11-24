using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Routes;

/// <summary>`routeCandidatesQuerySchema` : la liste entière, ou une page avec ses quartiers et villes.</summary>
public sealed record GetRouteCandidatesQuery(string Code, string? Search, string? District, string? CityId, string? Page, string? PageSize) : IRequest<object>;

internal sealed class GetRouteCandidatesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetRouteCandidatesQuery, object>
{
    public async Task<object> Handle(GetRouteCandidatesQuery request, CancellationToken cancellationToken)
    {
        var rows = await RouteWrites.CandidatesAsync(db, caller, request.Code, request.Search, cancellationToken);
        if (string.IsNullOrEmpty(request.Page)) return rows;
        var cities = rows.GroupBy(r => r.CityId).Select(g => new NamedIdDto(g.Key, g.First().CityName)).OrderBy(c => c.Name, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("fr"), false)).ToList();
        var inCity = RouteWrites.InCity(rows, request.CityId).ToList();
        var districts = new Dictionary<string, string>();
        foreach (var row in inCity)
        {
            var name = row.DestinationDistrictName?.Trim();
            if (!string.IsNullOrEmpty(name)) districts.TryAdd(CityNames.DistrictKey(name), name);
        }
        var page = RouteWrites.Page(RouteWrites.InDistrict(inCity, request.District).ToList(), request.Page, request.PageSize, r => r.HeldBy?.Locked == true);
        return new RouteCandidatePageDto(page.Items, page.Total, page.Page, page.PageSize, page.SelectableTotal,
            districts.Select(d => new NamedKeyDto(d.Key, d.Value)).OrderBy(d => d.Name, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("fr"), false)).ToList(), cities);
    }
}
