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

/// <summary>`chequeAdvanceSchema` : une transition ; le commentaire sert aussi de motif au rejet.</summary>
public sealed record ChequeTransitionCommand : IRequest<ChequeDetailDto>
{
    [JsonIgnore]
    public string Id { get; init; } = string.Empty;

    [JsonIgnore]
    public string Action { get; init; } = string.Empty;

    public string? Comment { get; init => field = Zod.Trim(value); }
}

public sealed class ChequeTransitionValidator : ZodValidator<ChequeTransitionCommand>
{
    public ChequeTransitionValidator()
    {
        RuleFor(x => x.Comment).MaxLength(500);
    }
}

internal sealed class ChequeTransitionHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<ChequeTransitionCommand, ChequeDetailDto>
{
    public async Task<ChequeDetailDto> Handle(ChequeTransitionCommand input, CancellationToken cancellationToken)
    {
        if (caller.ClientId is not null) throw BusinessException.Forbidden("Le suivi de vos chèques est en lecture seule.", new { reason = "cheque-is-agency-only" });
        var now = DateTime.UtcNow;
        switch (input.Action)
        {
            case "advance":
            {
                var cheque = await Cheques.LoadAsync(db, caller, input.Id, cancellationToken);
                if (caller.CourierId is not null && cheque.Status != "recu-livreur")
                {
                    throw BusinessException.Forbidden("Le chèque n’est plus dans votre sacoche : seule l’agence le fait avancer.", new { reason = "cheque-is-agency-only" });
                }
                var next = Cheques.Next(cheque.Status) ?? throw BusinessException.Conflict("Ce chèque est au bout de son parcours.", new { reason = "cheque-completed" });
                await Cheques.WriteAsync(db, caller, cheque.Id, next, input.Comment, now, null, cancellationToken);
                break;
            }
            case "reject":
            {
                if (caller.CourierId is not null) throw BusinessException.Forbidden("Seule l’agence constate un rejet.", new { reason = "cheque-is-agency-only" });
                var cheque = await Cheques.LoadAsync(db, caller, input.Id, cancellationToken);
                if (!Cheques.CanReject(cheque.Status)) throw BusinessException.Conflict("Ce chèque ne peut pas être rejeté à ce stade.", new { reason = "cheque-wrong-status", status = cheque.Status });
                var reason = input.Comment?.Trim();
                if (string.IsNullOrEmpty(reason)) throw BusinessException.Validation("Un chèque rejeté demande un motif.", new { field = "comment", reason = "cheque-reason-required" });
                await Cheques.WriteAsync(db, caller, cheque.Id, "rejete", reason, now, reason, cancellationToken);
                await Cheques.ReturnToDueAsync(db, caller, cheque.ParcelId, $"Chèque rejeté : {reason}", cancellationToken);
                break;
            }
            default:
            {
                if (caller.CourierId is not null) throw BusinessException.Forbidden("Seule l’agence annule un chèque.", new { reason = "cheque-is-agency-only" });
                var cheque = await Cheques.LoadAsync(db, caller, input.Id, cancellationToken);
                if (!Cheques.CanCancel(cheque.Status))
                {
                    throw BusinessException.Conflict("Ce chèque est encaissé ou déjà sorti : il ne s’annule plus.", new { reason = "cheque-wrong-status", status = cheque.Status });
                }
                await Cheques.WriteAsync(db, caller, cheque.Id, "annule", input.Comment, now, null, cancellationToken);
                await Cheques.ReturnToDueAsync(db, caller, cheque.ParcelId, string.IsNullOrWhiteSpace(input.Comment) ? "Chèque annulé." : input.Comment.Trim(), cancellationToken);
                break;
            }
        }
        db.ChangeTracker.Clear();
        return await Cheques.DetailAsync(db, caller, input.Id, cancellationToken);
    }
}
