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

/// <summary>`chequeGroupSlipSchema` : un bon de remise par vendeur (BRC-), en un seul PDF.</summary>
public sealed record GetChequeGroupSlipQuery : IRequest<FileContent>
{
    public IReadOnlyList<string>? Ids { get; init; }
}

public sealed class GetChequeGroupSlipValidator : ZodValidator<GetChequeGroupSlipQuery>
{
    public GetChequeGroupSlipValidator()
    {
        RuleFor(x => x.Ids).Required("array").Custom((ids, context) =>
        {
            if (ids is null) return;
            if (ids.Count < 1) context.AddFailure(ValidationPaths.Raw + "ids", "required");
            if (ids.Count > 500) context.AddFailure(ValidationPaths.Raw + "ids", "too-many");
            for (var i = 0; i < ids.Count; i++) if (string.IsNullOrEmpty(ids[i])) context.AddFailure(ValidationPaths.Raw + $"ids.{i}", Zod.TooSmallString(1));
        });
    }
}

internal sealed class GetChequeGroupSlipHandler(IAppDbContext db, ICurrentUser caller, IPdfService pdf, IFileStorage storage, IClock clock) : IRequestHandler<GetChequeGroupSlipQuery, FileContent>
{
    public async Task<FileContent> Handle(GetChequeGroupSlipQuery request, CancellationToken cancellationToken)
    {
        if (caller.ClientId is not null) throw BusinessException.Forbidden("Le bon de remise est édité par l’agence.");
        var ids = request.Ids!.Distinct().ToList();
        var cheques = await Cheques.SlipScoped(db.Cheques.AsNoTracking(), caller).Where(c => ids.Contains(c.Id))
            .Select(c => new
            {
                c.Id, c.Status, c.Number, c.Bank, c.Amount, c.PayerName, c.CourierId, ParcelCode = c.Parcel.Code, ClientId = c.Parcel.Client.Id, Client = c.Parcel.Client.Name,
                Courier = c.Courier.FirstName + " " + c.Courier.LastName,
            }).ToListAsync(cancellationToken);
        if (cheques.Count != ids.Count) throw BusinessException.NotFound("Un ou plusieurs chèques sont introuvables ou hors de portée.");
        var french = StringComparer.Create(CultureInfo.GetCultureInfo("fr-FR"), CompareOptions.None);
        var groups = cheques.GroupBy(c => c.ClientId).Select(g => new { Name = g.First().Client, Cheques = g.OrderBy(c => c.ParcelCode, StringComparer.Create(CultureInfo.InvariantCulture, false)).ToList() })
            .OrderBy(g => g.Name, french).ToList();
        var (company, logo) = await ChequeSlips.LetterheadAsync(db, storage, cancellationToken);
        var printedAt = clock.UtcNow;
        var codes = new List<string>();
        foreach (var _ in groups) codes.Add(await DocumentCodes.ReserveAsync(db, ChequeSlips.Prefix, printedAt, cancellationToken));
        var slips = groups.Select((g, index) => new ChequeGroupSlip(codes[index], g.Name, g.Cheques.Select(c => c.CourierId).Distinct().Count() == 1 ? g.Cheques[0].Courier : null,
            g.Cheques.Select(c => new ChequeGroupSlipLine(c.Number, c.Bank, Money.ToCents(c.Amount), c.ParcelCode, c.Courier, c.PayerName)).ToList())).ToList();
        var content = pdf.Render(new ChequeGroupSlipSheet(company, logo, printedAt, slips));
        var authorName = await Remittances.AuthorNameAsync(db, caller.UserId, cancellationToken);
        for (var i = 0; i < groups.Count; i++)
        {
            foreach (var cheque in groups[i].Cheques)
            {
                db.ChequeEvents.Add(new ChequeEvent
                {
                    ChequeId = cheque.Id, Status = cheque.Status, Kind = "slip", Comment = $"Bon de remise groupé {codes[i]} téléchargé", AuthorId = caller.UserId, AuthorName = authorName,
                    CreatedAt = printedAt,
                });
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        var fileName = codes.Count == 1 ? $"{codes[0]}.pdf" : $"{codes[0]}-{codes.Count}-bons.pdf";
        return new FileContent(content, FileContent.Pdf, fileName, CacheControl: "private, no-store");
    }
}
