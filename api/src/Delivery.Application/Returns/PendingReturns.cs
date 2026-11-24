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

public static class PendingReturns
{
    private static IQueryable<Parcel> Search(IQueryable<Parcel> where, string? search)
    {
        if (string.IsNullOrEmpty(search)) return where;
        var needle = search.ToLower();
        return where.Where(p => p.Code.ToLower().Contains(needle) || p.RecipientName.ToLower().Contains(needle) || p.RecipientPhone.ToLower().Contains(needle));
    }

    public static async Task<IReadOnlyList<ReturnPendingGroupDto>> ListAsync(IAppDbContext db, ICurrentUser caller, GetPendingReturnsQuery query, CancellationToken cancellationToken)
    {
        var excluded = StatusCatalog.ReturnExcludedStatusIds;
        if (caller.ClientId is not null && !caller.IsSuperAdmin())
        {
            var merchant = ParcelFilters.Scoped(db.Parcels.AsNoTracking(), caller).Where(p => !excluded.Contains(p.StatusId)
                && (StatusCatalog.MerchantReturnStatusIds.Contains(p.StatusId) || p.Kind == ParcelKind.RETURN
                    || p.HandoverItems.Any(i => i.CorrectedAt == null && (i.Handover.Status == ReturnHandoverStatus.CONFIRMED || i.Handover.Status == ReturnHandoverStatus.CLOSED))
                    || p.SlipItems.Any()));
            var lines = await ReturnRows.Lines(Search(merchant, query.Search).OrderByDescending(p => p.StatusAt).ThenBy(p => p.Code).Take(NoteRules.SelectionMax + 1)).ToListAsync(cancellationToken);
            return lines.Count == 0 ? [] : [new ReturnPendingGroupDto(null, null, null, null, lines.Select(l => l with { CourierName = null }).ToList())];
        }
        var where = ReturnRows.NotReserved(ParcelFilters.Scoped(db.Parcels.AsNoTracking(), caller).Where(p => !excluded.Contains(p.StatusId)));
        if (!string.IsNullOrEmpty(query.AgencyId) && caller.MayFilterBy(caller.AgencyId)) where = where.Where(p => p.AgencyId == query.AgencyId);
        if (!string.IsNullOrEmpty(query.CourierId) && caller.MayFilterBy(caller.CourierId)) where = where.Where(p => p.CourierId == query.CourierId);
        var rows = await Search(where, query.Search).OrderBy(p => p.CourierId).ThenBy(p => p.StatusAt).Take(NoteRules.SelectionMax + 1)
            .Select(p => new { p.CourierId, AgencyId = (string?)p.Agency.Id, AgencyName = (string?)p.Agency.Name, p.Id }).ToListAsync(cancellationToken);
        var ids = rows.Select(r => r.Id).ToList();
        var lineById = (await ReturnRows.Lines(db.Parcels.AsNoTracking().Where(p => ids.Contains(p.Id))).ToListAsync(cancellationToken)).ToDictionary(l => l.Id);
        var groups = new List<(string Key, ReturnPendingGroupDto Group, List<ReturnParcelLineDto> Parcels)>();
        foreach (var row in rows)
        {
            var key = row.CourierId ?? "__none__";
            var index = groups.FindIndex(g => g.Key == key);
            var line = lineById[row.Id];
            if (index < 0) groups.Add((key, new ReturnPendingGroupDto(row.CourierId, line.CourierName, row.AgencyId, row.AgencyName, []), [line]));
            else groups[index].Parcels.Add(line);
        }
        var compare = StringComparer.Create(System.Globalization.CultureInfo.InvariantCulture, false);
        return groups.Select(g => g.Group with { Parcels = g.Parcels })
            .OrderBy(g => g.CourierId is null ? 0 : 1).ThenBy(g => g.CourierName ?? string.Empty, compare).ToList();
    }
}
