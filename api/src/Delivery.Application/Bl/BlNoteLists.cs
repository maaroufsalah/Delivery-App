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

internal static class BlNoteLists
{
    public static IQueryable<BlNote> Filter(IAppDbContext db, ICurrentUser caller, BlNoteFilter filter)
    {
        var where = BlRows.Notes(db.BlNotes.AsNoTracking(), caller);
        if (filter.Status is { } statuses)
        {
            var values = statuses.Select(s => Enum.Parse<BlNoteStatus>(Money.FromWire(s))).ToList();
            where = where.Where(n => values.Contains(n.Status));
        }
        if (filter.ClientId is { } clients && caller.MayFilterBy(caller.ClientId)) where = where.Where(n => clients.Contains(n.ClientId));
        if (filter.CourierId is { } couriers && caller.MayFilterBy(caller.CourierId)) where = where.Where(n => couriers.Contains(n.CourierId));
        if (filter.AgencyId is { } agencies && caller.MayFilterBy(caller.AgencyId)) where = where.Where(n => agencies.Contains(n.AgencyId));
        if (filter.Paging.Search is { Length: > 0 } search)
        {
            var needle = search.ToLower();
            where = where.Where(n => n.Code.ToLower().Contains(needle) || n.Client.Name.ToLower().Contains(needle));
        }
        return filter.Paging.Order == "asc" ? where.OrderBy(n => n.CreatedAt) : where.OrderByDescending(n => n.CreatedAt);
    }

    public static void Validate(BlNoteFilter filter)
    {
        var bad = filter.Status?.Where(s => !BlRows.NoteStatuses.Contains(s)).ToList();
        if (bad is { Count: > 0 })
        {
            throw BusinessException.Validation("Saisie invalide.", new { issues = new[] { new { path = "status", message = Zod.InvalidOption(BlRows.NoteStatuses) } } });
        }
    }
}
