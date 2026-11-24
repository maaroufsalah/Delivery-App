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
using Delivery.Application.Returns;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Finance;

internal static class ChequeLists
{
    public static IQueryable<Cheque> Filter(IAppDbContext db, ICurrentUser caller, ChequeListFilter filter)
    {
        var where = Cheques.Scoped(db.Cheques.AsNoTracking(), caller);
        if (filter.Status is { } statuses)
        {
            var wanted = statuses.SelectMany(s => s == Cheques.PendingFilter ? ["recu-livreur", "remis-admin", "recu-admin", "transmis-vendeur"] : new[] { s }).Distinct()
                .Select(s => Enum.TryParse<ChequeStatus>(Money.FromWire(s), out var value) ? value : (ChequeStatus?)null).ToList();
            // Un statut inconnu ne sélectionne rien (Speedy le laisse remonter en erreur Prisma).
            var known = wanted.Where(v => v is not null).Select(v => v!.Value).ToList();
            where = where.Where(c => known.Contains(c.Status));
        }
        if (filter.CourierId is { } couriers && caller.MayFilterBy(caller.CourierId)) where = where.Where(c => couriers.Contains(c.CourierId));
        if (filter.ClientId is { } clients && caller.MayFilterBy(caller.ClientId)) where = where.Where(c => clients.Contains(c.Parcel.ClientId));
        if (!string.IsNullOrEmpty(filter.Bank)) { var bank = filter.Bank.ToLower(); where = where.Where(c => c.Bank != null && c.Bank.ToLower().Contains(bank)); }
        var (from, to) = ReturnRows.DayRange(filter.From, filter.To);
        if (from is { } f) where = where.Where(c => c.ReceivedAt >= f);
        if (to is { } t) where = where.Where(c => c.ReceivedAt < t);
        if (filter.Paging.Search is { Length: > 0 } search)
        {
            var needle = search.ToLower();
            where = where.Where(c => c.Number.ToLower().Contains(needle) || c.Parcel.Code.ToLower().Contains(needle));
        }
        return where;
    }
}
