using System.Text.Json;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.Parcels.Commands;
using Delivery.Application.Parcels.Labels;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Routes;

public sealed record FieldStatusChangeDto(string Code, int From, int To);

public sealed record FieldStatusRefusalDto(string Code, string Reason, string Message);

public sealed record AdminFieldStatusResultDto(IReadOnlyList<FieldStatusChangeDto> Changed, IReadOnlyList<FieldStatusRefusalDto> Refused);

/// <summary>`adminFieldStatusSchema` (D-245) : l'administrateur revoit un statut de terrain, sur un colis ou une sélection (300 au plus).</summary>
public sealed record AdminFieldStatusCommand : LabelSelection, IRequest<AdminFieldStatusResultDto>
{
    public const int Max = 300;

    public JsonElement? StatusId { get; init; }
    public string? Comment { get; init => field = Zod.Trim(value); }
    public string? ScheduledDate { get; init; }

    public int StatusIdValue => (int)JsCoerce.Number(StatusId);
}

public sealed class AdminFieldStatusValidator : LabelSelectionValidator<AdminFieldStatusCommand>
{
    public AdminFieldStatusValidator()
    {
        RuleFor(x => x.StatusId).Custom((value, context) =>
        {
            var number = JsCoerce.Number(value);
            var issue = double.IsNaN(number) ? "Invalid input: expected number, received NaN"
                : Math.Floor(number) != number || double.IsInfinity(number) ? Zod.ExpectedInt
                : number <= 0 ? "required"
                : null;
            if (issue is not null) context.AddFailure(ValidationPaths.Raw + "statusId", issue);
        });
        RuleFor(x => x.Comment).Required().MinLength(1, "required").MaxLength(500);
    }
}

internal sealed class AdminFieldStatusHandler(IAppDbContext db, ICurrentUser caller, IClock clock, ILogger<AdminFieldStatusHandler> logger)
    : IRequestHandler<AdminFieldStatusCommand, AdminFieldStatusResultDto>
{
    public async Task<AdminFieldStatusResultDto> Handle(AdminFieldStatusCommand input, CancellationToken cancellationToken)
    {
        if (!caller.IsSuperAdmin()) throw BusinessException.Forbidden("Seul un administrateur change librement un statut de terrain.", new { reason = "field-status-admin-only" });
        var to = input.StatusIdValue;
        if (to == RouteStatusIds.Delivered)
        {
            throw BusinessException.Conflict("« Livré » se pose depuis le circuit du colis : c’est lui qui encaisse et remet les fonds.", new { reason = "deliver-through-route" });
        }
        var allowed = StatusCatalog.AdminFieldStatusIds();
        if (!allowed.Contains(to))
        {
            throw BusinessException.Transition("Ce statut ne se pose pas à la main : il naît d’un bon, d’un circuit ou d’un encaissement.", new { reason = "target-not-a-field-status", to, allowed });
        }
        var codes = await LabelSelections.CodesAsync(db, caller, clock, input, cancellationToken);
        if (codes.Count > AdminFieldStatusCommand.Max)
        {
            throw BusinessException.Validation($"{AdminFieldStatusCommand.Max} colis au plus par geste : la sélection en compte {codes.Count}. Affinez les filtres.",
                new { reason = "selection-too-large", max = AdminFieldStatusCommand.Max, count = codes.Count });
        }
        var changed = new List<FieldStatusChangeDto>();
        var refused = new List<FieldStatusRefusalDto>();
        foreach (var code in codes)
        {
            try
            {
                changed.Add(new FieldStatusChangeDto(code, await ApplyOneAsync(code, input, to, cancellationToken), to));
            }
            catch (BusinessException error)
            {
                db.ChangeTracker.Clear();
                refused.Add(new FieldStatusRefusalDto(code, error.Details?.GetType().GetProperty("reason")?.GetValue(error.Details) as string ?? "refused", error.Message));
            }
        }
        return new AdminFieldStatusResultDto(changed, refused);
    }

    private async Task<int> ApplyOneAsync(string code, AdminFieldStatusCommand input, int to, CancellationToken cancellationToken)
    {
        var parcel = await db.Parcels.AsNoTracking().Where(p => p.Code == code && p.DeletedAt == null)
                         .Select(p => new { p.Id, p.StatusId, p.BillingState, p.FinancialStatus, p.ReimbursedByCourier }).FirstOrDefaultAsync(cancellationToken)
                     ?? throw BusinessException.NotFound($"Colis introuvable : {code}");
        var from = parcel.StatusId;
        if (from == RouteStatusIds.Delivered)
        {
            throw BusinessException.Conflict("Ce colis est livré : son statut ne change plus ici (« Annuler la livraison » a son propre chemin).", new { reason = "parcel-delivered" });
        }
        if (!StatusCatalog.AdminFieldStatusIds().Contains(from))
        {
            throw BusinessException.Transition("Le statut actuel de ce colis vient d’un bon, d’un circuit ou d’un encaissement : il ne se change pas à la main.", new { reason = "not-a-field-status", from });
        }
        if (!StatusCatalog.CanAdminReviseField(from, to)) throw BusinessException.Conflict("Ce colis porte déjà ce statut.", new { reason = "same-status", from });
        if (parcel.BillingState != BillingState.TO_INVOICE)
        {
            throw BusinessException.Conflict("Ce colis est déjà facturé : son statut ne change plus (passez par un avoir).", new { reason = "parcel-invoiced" });
        }
        var money = parcel.FinancialStatus != FinancialStatus.NON_ENCAISSE ? "encaissement enregistré"
            : parcel.ReimbursedByCourier ? "remis par le livreur"
            : await db.CashRemittanceParcels.AsNoTracking().Where(l => l.ParcelId == parcel.Id && l.Remittance.Status != CashRemittanceStatus.CANCELLED)
                  .Select(l => "caisse " + l.Remittance.Code).FirstOrDefaultAsync(cancellationToken)
              ?? await db.CodPayoutParcels.AsNoTracking().Where(l => l.ParcelId == parcel.Id).Select(l => "bon de versement " + l.Payout.Code).FirstOrDefaultAsync(cancellationToken);
        if (money is not null) throw BusinessException.Conflict($"De l’argent est attaché à ce colis ({money}) : son statut ne change plus ici.", new { reason = "parcel-money-collected" });
        var handover = await db.ReturnHandoverItems.AsNoTracking()
            .Where(i => i.ReservedParcelId == parcel.Id && i.Handover.Status != ReturnHandoverStatus.CONFIRMED && i.Handover.Status != ReturnHandoverStatus.CLOSED
                        && i.Handover.Status != ReturnHandoverStatus.CANCELLED)
            .Select(i => i.Handover.Code).FirstOrDefaultAsync(cancellationToken);
        if (handover is not null) throw BusinessException.Conflict($"Ce colis est sur le bon de retour {handover} : son statut ne change plus.", new { reason = "parcel-on-handover", handover });
        var onRoute = await db.RouteParcels.AsNoTracking().Where(l => l.ParcelId == parcel.Id && l.Route.Status != RouteStatus.DRAFT && l.Route.Status != RouteStatus.CLOSED)
            .Select(l => new { l.Route.Code, l.Route.Kind }).FirstOrDefaultAsync(cancellationToken);
        if (onRoute is not null && onRoute.Kind != RouteKind.DELIVERY)
        {
            throw BusinessException.Conflict($"Ce colis est sur le circuit {onRoute.Code}, parti : posez son issue depuis le circuit.", new { reason = "parcel-on-route", routeCode = onRoute.Code });
        }
        if (onRoute is not null)
        {
            await RouteParcelConfirm.RunAsync(db, caller, logger, onRoute.Code, parcel.Id, new RouteOutcome(true, to, input.ScheduledDate ?? string.Empty, input.Comment), true, cancellationToken);
        }
        else
        {
            await ParcelStatusChange.ChangeAsync(db, caller, clock, logger, code, new StatusChange(to, input.Comment, input.ScheduledDate), true, cancellationToken);
        }
        try
        {
            db.AuditLogs.Add(new AuditLog
            {
                Entity = "Parcel", EntityId = parcel.Id, Action = AuditAction.UPDATE, Before = JsonSerializer.Serialize(new { statusId = from }),
                After = JsonSerializer.Serialize(new { statusId = to, adminFieldStatus = true, summary = $"{input.Comment}{(onRoute is null ? string.Empty : $" (circuit {onRoute.Code})")}" }),
                Diff = ["statusId"], UserId = caller.UserId, At = clock.UtcNow,
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException error)
        {
            db.ChangeTracker.Clear();
            logger.LogError(error, "Journal du statut de terrain non écrit pour le colis {Parcel} ({From} → {To})", parcel.Id, from, to);
        }
        return from;
    }
}
