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

/// <summary>`POST /cheques/:id/slip` : le bon de remise d'un chèque (code BRC-&lt;colis&gt;), tracé dans son journal.</summary>
public sealed record GetChequeSlipQuery(string Id) : IRequest<FileContent>;

internal sealed class GetChequeSlipHandler(IAppDbContext db, ICurrentUser caller, IPdfService pdf, IFileStorage storage, IClock clock) : IRequestHandler<GetChequeSlipQuery, FileContent>
{
    public async Task<FileContent> Handle(GetChequeSlipQuery request, CancellationToken cancellationToken)
    {
        if (caller.ClientId is not null) throw BusinessException.Forbidden("Le bon de remise est édité par l’agence.");
        var cheque = await Cheques.SlipScoped(db.Cheques.AsNoTracking(), caller).Where(c => c.Id == request.Id)
                         .Select(c => new
                         {
                             c.Id, c.Status, c.Number, c.Bank, c.Amount, ParcelCode = c.Parcel.Code, c.Parcel.RecipientName, Client = c.Parcel.Client.Name,
                             Courier = c.Courier.FirstName + " " + c.Courier.LastName,
                         }).FirstOrDefaultAsync(cancellationToken)
                     ?? throw BusinessException.NotFound($"Chèque introuvable : {request.Id}");
        var (company, logo) = await ChequeSlips.LetterheadAsync(db, storage, cancellationToken);
        var code = $"{ChequeSlips.Prefix}-{cheque.ParcelCode}";
        var printedAt = clock.UtcNow;
        var content = pdf.Render(new ChequeSlipSheet(company, logo, code, printedAt, cheque.Number, cheque.Bank, Money.ToCents(cheque.Amount), cheque.ParcelCode, cheque.Client, cheque.Courier,
            cheque.RecipientName));
        db.ChequeEvents.Add(new ChequeEvent
        {
            ChequeId = cheque.Id, Status = cheque.Status, Kind = "slip", Comment = $"Bon de remise {code} téléchargé", AuthorId = caller.UserId,
            AuthorName = await Remittances.AuthorNameAsync(db, caller.UserId, cancellationToken), CreatedAt = printedAt,
        });
        await db.SaveChangesAsync(cancellationToken);
        return new FileContent(content, FileContent.Pdf, $"{code}.pdf", CacheControl: "private, no-store");
    }
}
