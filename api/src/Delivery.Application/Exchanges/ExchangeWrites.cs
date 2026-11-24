using AutoMapper;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Exchanges.Commands;
using Delivery.Application.Exchanges.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Exchanges;

/// <summary>Écritures partagées d'`ExchangesService` : ouverture du dossier, avancement.</summary>
public static class ExchangeWrites
{
    /// <summary>
    /// Le COD (en dirhams, comme la création de colis l'attend) du nouveau colis d'un échange
    /// dont le montant arrive en centimes. Speedy passait les centimes tels quels (COD ×100).
    /// </summary>
    public static double CodMadOf(long amountCents) => amountCents / 100.0;

    /// <summary>Le dossier `DEMANDE` et son premier événement, sous un code `ECH-AAMM-0001`.</summary>
    public static async Task<Exchange> OpenAsync(IAppDbContext db, ICurrentUser caller, Parcel original, string returnedProduct, string newProduct, string newDestination,
        long amountCents, long feesCents, string? courierId, string? note, string firstComment, DateTime now, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var code = await DocumentCodes.ReserveAsync(db, DocumentCodes.Exchange, now, cancellationToken);
        var exchange = new Exchange
        {
            Code = code,
            OriginalParcelId = original.Id,
            ReturnedProduct = returnedProduct,
            NewProduct = newProduct,
            OldDestination = $"{original.DeliveryAddress} — {original.DestinationCity.Name}",
            NewDestination = newDestination,
            Amount = Money.FromCents(amountCents),
            Fees = Money.FromCents(feesCents),
            CourierId = courierId,
            Note = string.IsNullOrEmpty(note) ? null : note,
            RequestedAt = now,
            CreatedById = caller.UserId,
        };
        db.Exchanges.Add(exchange);
        db.ExchangeEvents.Add(new ExchangeEvent
        {
            ExchangeId = exchange.Id,
            Status = ExchangeStatus.DEMANDE,
            Comment = firstComment,
            AuthorId = caller.UserId,
            AuthorName = await ExchangeRows.AuthorNameAsync(db, caller.UserId, cancellationToken),
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return exchange;
    }

    public static async Task<ExchangeDetailDto> AdvanceAsync(IAppDbContext db, ICurrentUser caller, IMapper mapper, ExchangeStepCommand input, ExchangeStatus target, ExchangeStatus expected,
        Action<Exchange, DateTime> columns, CancellationToken cancellationToken)
    {
        ExchangeRows.AssertAgency(caller, "Seule l’agence fait avancer un échange.");
        var exchange = await ExchangeRows.Scoped(db.Exchanges, caller).FirstOrDefaultAsync(x => x.Code == input.Code, cancellationToken)
                       ?? throw BusinessException.NotFound($"Échange introuvable : {input.Code}");
        if (exchange.Status != expected)
        {
            var wire = Money.ToWire(exchange.Status);
            throw BusinessException.Conflict($"Cet échange est en « {wire} » : ce geste ne s’y applique pas.", new { reason = "exchange-wrong-status", status = wire });
        }
        var now = DateTime.UtcNow;
        await WriteAsync(db, caller, exchange, target, input.Comment, x => columns(x, now), cancellationToken);
        return await ExchangeRows.DetailAsync(db, caller, mapper, input.Code, cancellationToken);
    }

    public static async Task WriteAsync(IAppDbContext db, ICurrentUser caller, Exchange exchange, ExchangeStatus status, string? comment, Action<Exchange> columns, CancellationToken cancellationToken)
    {
        var author = await ExchangeRows.AuthorNameAsync(db, caller.UserId, cancellationToken);
        exchange.Status = status;
        columns(exchange);
        db.ExchangeEvents.Add(new ExchangeEvent
        {
            ExchangeId = exchange.Id,
            Status = status,
            Comment = string.IsNullOrEmpty(comment) ? null : comment,
            AuthorId = caller.UserId,
            AuthorName = author,
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
