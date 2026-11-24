using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Idempotency;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.CourierApp;
using Delivery.Application.Parcels;
using Delivery.Application.Routes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using Delivery.Domain.Parcels;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Providers;

/// <summary>
/// `ProviderStatusService.handle` : chaque appel est journalisé (`provider_calls`) ; un rejeu de la même clé rend le
/// premier résultat ; un statut plus ancien que le dernier appliqué est ignoré (STALE) ; en `LOG_ONLY`, rien ne
/// s'écrit (WOULD_APPLY / WOULD_REJECT). L'issue passe par le circuit du livreur quand il peut la poser, sinon par la
/// correction de statut, en miroir (bons, circuits et retours gardés).
/// </summary>
public static class ProviderStatusReport
{
    private static readonly ProviderCallOutcome[] Ordering = [ProviderCallOutcome.APPLIED, ProviderCallOutcome.WOULD_APPLY, ProviderCallOutcome.ALREADY_APPLIED];

    private static readonly ProviderCallOutcome[] Replayable =
        [ProviderCallOutcome.APPLIED, ProviderCallOutcome.WOULD_APPLY, ProviderCallOutcome.ALREADY_APPLIED, ProviderCallOutcome.IGNORED, ProviderCallOutcome.STALE, ProviderCallOutcome.WOULD_REJECT];

    public static bool IsIsoWithOffset(string value) =>
        System.Text.RegularExpressions.Regex.IsMatch(value, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d+)?)?(Z|[+-]\d{2}:\d{2})$")
        && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private sealed class Call(IAppDbContext db, ILogger logger, ProviderContext provider, ProviderCall entry)
    {
        public ProviderCall Entry { get; } = entry;

        public async Task<ProviderStatusResultDto> DoneAsync(ProviderCallOutcome outcome, string? reason, int? statusId, bool alert = false)
        {
            await RecordAsync(outcome, 200, reason, alert);
            return new ProviderStatusResultDto(outcome.ToString(), provider.Mode.ToString(), Entry.ParcelCode, Entry.ProviderStatus, Entry.MappedStatus, statusId) { Reason = reason };
        }

        public async Task<ProviderStatusResultDto> RefuseAsync(Exception error, int? statusId = null)
        {
            if (error is not BusinessException business)
            {
                await RecordAsync(ProviderCallOutcome.REJECTED, 500, "internal", true);
                throw error;
            }
            if (provider.Mode == ProviderMode.LOG_ONLY) return await DoneAsync(ProviderCallOutcome.WOULD_REJECT, ReasonOf(business), statusId, true);
            return await RejectAsync(business);
        }

        public async Task<ProviderStatusResultDto> RejectAsync(BusinessException error)
        {
            await RecordAsync(ProviderCallOutcome.REJECTED, StatusOf(error), ReasonOf(error), true);
            throw error;
        }

        private async Task RecordAsync(ProviderCallOutcome outcome, int httpStatus, string? reason, bool alert)
        {
            try
            {
                db.ChangeTracker.Clear();
                db.ProviderCalls.Add(new ProviderCall
                {
                    TokenId = Entry.TokenId, EventKey = Cut(Entry.EventKey), ParcelCode = Cut(Entry.ParcelCode)!, ParcelId = Entry.ParcelId, ProviderStatus = Entry.ProviderStatus,
                    MappedStatus = Entry.MappedStatus, OccurredAt = Entry.OccurredAt, Mode = Entry.Mode, Outcome = outcome, HttpStatus = httpStatus, Reason = Cut(reason), RequestId = Cut(Entry.RequestId),
                    Alert = alert, At = DateTime.UtcNow,
                });
                await db.SaveChangesAsync(CancellationToken.None);
            }
            catch (DbUpdateException error)
            {
                db.ChangeTracker.Clear();
                logger.LogWarning("Appel prestataire non journalisé ({Code}, {Outcome}) : {Message}", Entry.ParcelCode, outcome, error.Message);
            }
        }

        private static string? Cut(string? value) => value is null ? null : value.Length > 80 ? value[..80] : value;
    }

    public static string ReasonOf(BusinessException error) => error.Details switch
    {
        IReadOnlyDictionary<string, object?> map when map.TryGetValue("reason", out var r) && r is string s => s,
        { } details when details.GetType().GetProperty("reason")?.GetValue(details) is string s => s,
        _ => error.Code.ToString().ToLowerInvariant(),
    };

    private static int StatusOf(BusinessException error) => error.Code switch
    {
        ErrorCode.NOT_FOUND => 404,
        ErrorCode.FORBIDDEN => 403,
        ErrorCode.UNAUTHENTICATED => 401,
        ErrorCode.CONFLICT or ErrorCode.TRANSITION_FORBIDDEN => 409,
        ErrorCode.VALIDATION_FAILED => 422,
        ErrorCode.TOO_MANY_REQUESTS => 429,
        ErrorCode.PAYLOAD_TOO_LARGE => 413,
        _ => 500,
    };

    public static async Task<ProviderStatusResultDto> HandleAsync(IAppDbContext db, ICurrentUser caller, IClock clock, ILogger logger, ProviderStatusCommand input, CancellationToken cancellationToken)
    {
        var provider = input.Provider ?? throw BusinessException.Unauthenticated("Jeton de prestataire absent.");
        var code = input.Code.Trim();
        var providerStatus = (int)input.ProviderStatus!.Value.GetDouble();
        var mapped = ProviderStatusMap.Map(ProviderStatusMap.Effective(provider.StatusMap), providerStatus);
        var occurredAt = DateTimeOffset.Parse(input.OccurredAt!, CultureInfo.InvariantCulture).UtcDateTime;
        var call = new Call(db, logger, provider, new ProviderCall
        {
            TokenId = provider.TokenId, EventKey = input.EventKey, ParcelCode = code, ProviderStatus = providerStatus, MappedStatus = mapped, OccurredAt = occurredAt, Mode = provider.Mode,
            RequestId = caller.RequestId,
        });
        if (string.IsNullOrEmpty(input.EventKey))
        {
            return await call.RejectAsync(BusinessException.Validation("En-tête Idempotency-Key obligatoire.", new { reason = "idempotency-key-required" }));
        }
        var seen = await db.ProviderCalls.AsNoTracking().Where(c => c.TokenId == provider.TokenId && c.EventKey == input.EventKey && c.Mode == provider.Mode && Replayable.Contains(c.Outcome))
            .OrderBy(c => c.At).FirstOrDefaultAsync(cancellationToken);
        if (seen is not null)
        {
            if (seen.ParcelCode != code) return await call.RejectAsync(BusinessException.Validation("Cette clé d’envoi a déjà servi pour un autre colis.", new { reason = "idempotency-key-reused" }));
            call.Entry.ParcelId = seen.ParcelId;
            return await call.DoneAsync(seen.Outcome, seen.Reason ?? "replayed", seen.Outcome == ProviderCallOutcome.APPLIED ? seen.MappedStatus : null);
        }
        if (mapped is null)
        {
            var deliveredRef = await DeliveredByAsync(db, caller, code, cancellationToken);
            if (deliveredRef is not null)
            {
                call.Entry.ParcelId = deliveredRef.Id;
                return await call.DoneAsync(ProviderCallOutcome.IGNORED, "ignored-after-delivery", deliveredRef.StatusId, alert: true);
            }
            return await call.DoneAsync(ProviderCallOutcome.IGNORED, ProviderStatusMap.ProviderStatusIds.Contains(providerStatus) ? "status-ignored" : "unknown-provider-status", null);
        }
        CourierParcels.Located? located = null;
        BusinessException? notOnRoute = null;
        try
        {
            located = await CourierParcels.LocateAsync(db, caller, code, cancellationToken);
        }
        catch (BusinessException error) when (ReasonOf(error) == "parcel-not-mine")
        {
            notOnRoute = error;
        }
        catch (BusinessException error)
        {
            return await call.RefuseAsync(error);
        }
        var parcel = located is null ? null : new ParcelRef(located.ParcelId, located.Code, located.StatusId);
        if (parcel is null || parcel.StatusId == CourierPay.Delivered)
        {
            parcel = await DeliveredByAsync(db, caller, code, cancellationToken) ?? parcel ?? await HeldByAsync(db, caller, code, cancellationToken);
        }
        if (parcel is null) return await call.RefuseAsync(notOnRoute ?? NotMine(code));
        call.Entry.ParcelId = parcel.Id;
        if (parcel.StatusId == mapped) return await call.DoneAsync(ProviderCallOutcome.ALREADY_APPLIED, null, parcel.StatusId);
        var stale = await db.ProviderCalls.AsNoTracking().AnyAsync(c => c.TokenId == provider.TokenId && c.ParcelId == parcel.Id && c.Mode == provider.Mode && Ordering.Contains(c.Outcome)
                                                                         && c.OccurredAt > occurredAt, cancellationToken);
        if (stale) return await call.DoneAsync(ProviderCallOutcome.STALE, "older-than-last-applied", parcel.StatusId);
        var collected = input.CollectedAmount is { ValueKind: JsonValueKind.Number } amount ? amount.GetDouble() : (double?)null;
        if (mapped == CourierPay.Delivered)
        {
            try
            {
                await AssertDeliveryPaymentAsync(db, caller, parcel.Id, collected, cancellationToken);
            }
            catch (BusinessException error)
            {
                return await call.RefuseAsync(error, parcel.StatusId);
            }
        }
        var courierGate = located is not null && parcel.StatusId != CourierPay.Delivered && StatusCatalog.CourierDeliveryOutcomeIds.Contains(mapped.Value)
                          && (await CourierParcels.OutcomesAsync(db, caller, code, cancellationToken)).Contains(mapped.Value);
        if (!courierGate) return await CorrectAsync(db, caller, clock, logger, provider, call, parcel, mapped.Value, providerStatus, input, cancellationToken);
        if (provider.Mode == ProviderMode.LOG_ONLY) return await call.DoneAsync(ProviderCallOutcome.WOULD_APPLY, null, parcel.StatusId);
        var outcome = new RouteOutcome(true, mapped, input.ScheduledAt ?? string.Empty, string.IsNullOrEmpty(input.Comment) ? $"{provider.Name} — statut {providerStatus}" : input.Comment,
            mapped == CourierPay.Delivered ? collected : null);
        try
        {
            await IdempotentReplay.RunAsync(db, caller, input.EventKey, $"POST /provider/parcels/{code}/status",
                () => CourierParcels.ReportAsync(db, caller, logger, code, outcome, cancellationToken), cancellationToken);
        }
        catch (BusinessException error)
        {
            return await call.RefuseAsync(error, parcel.StatusId);
        }
        return await call.DoneAsync(ProviderCallOutcome.APPLIED, null, mapped);
    }

    private sealed record ParcelRef(string Id, string Code, int StatusId);

    private static async Task<ProviderStatusResultDto> CorrectAsync(IAppDbContext db, ICurrentUser caller, IClock clock, ILogger logger, ProviderContext provider, Call call, ParcelRef parcel, int mapped,
        int providerStatus, ProviderStatusCommand input, CancellationToken cancellationToken)
    {
        var fromDelivered = parcel.StatusId == CourierPay.Delivered;
        var elevated = new ElevatedCaller(caller);
        var reasonText = string.Join(" — ", new[]
        {
            fromDelivered ? $"{provider.Name} : statut {providerStatus} reçu sur un colis livré" : $"{provider.Name} : statut {providerStatus}", input.Comment,
        }.Where(t => !string.IsNullOrEmpty(t)));
        if (reasonText.Length > 500) reasonText = reasonText[..500];
        var correction = new CorrectionInput(mapped, reasonText, caller.CourierId, null, string.IsNullOrEmpty(input.ScheduledAt) ? null : input.ScheduledAt, null);
        var reason = fromDelivered ? "correction-after-delivery" : "correction";
        if (provider.Mode == ProviderMode.LOG_ONLY)
        {
            try
            {
                var preview = await StatusCorrection.PreviewAsync(db, parcel.Code, correction, cancellationToken, keep: true);
                if (preview.NeedsCourier) return await call.DoneAsync(ProviderCallOutcome.WOULD_REJECT, "courier-required", parcel.StatusId, alert: true);
                return await call.DoneAsync(ProviderCallOutcome.WOULD_APPLY, reason, parcel.StatusId);
            }
            catch (BusinessException error)
            {
                return await call.RefuseAsync(error, parcel.StatusId);
            }
        }
        try
        {
            await IdempotentReplay.RunAsync(db, caller, input.EventKey, $"POST /provider/parcels/{parcel.Code}/correction",
                () => StatusCorrection.ApplyAsync(db, elevated, clock, logger, parcel.Code, correction, cancellationToken, keep: true), cancellationToken);
        }
        catch (BusinessException error)
        {
            return await call.RefuseAsync(error, parcel.StatusId);
        }
        return await call.DoneAsync(ProviderCallOutcome.APPLIED, reason, mapped);
    }

    private static Task<ParcelRef?> HeldByAsync(IAppDbContext db, ICurrentUser caller, string code, CancellationToken cancellationToken)
    {
        if (caller.CourierId is not { } courierId) return Task.FromResult<ParcelRef?>(null);
        return ParcelLookup.ResolveAsync(db.Parcels.AsNoTracking().Where(p => p.DeletedAt == null && p.CourierId == courierId), code,
            matched => matched.Select(p => new ParcelRef(p.Id, p.Code, p.StatusId)).FirstOrDefaultAsync(cancellationToken));
    }

    private static Task<ParcelRef?> DeliveredByAsync(IAppDbContext db, ICurrentUser caller, string code, CancellationToken cancellationToken)
    {
        if (caller.CourierId is not { } courierId) return Task.FromResult<ParcelRef?>(null);
        return ParcelLookup.ResolveAsync(db.Parcels.AsNoTracking().Where(p => p.DeletedAt == null && p.StatusId == CourierPay.Delivered), code, async matched =>
        {
            var ids = matched.Select(p => p.Id);
            return await db.RouteParcels.AsNoTracking()
                .Where(l => ids.Contains(l.ParcelId) && l.FinalStatusId == CourierPay.Delivered && l.Route.CourierId == courierId && l.Route.Kind == RouteKind.DELIVERY)
                .OrderByDescending(l => l.RouteId).Select(l => new ParcelRef(l.Parcel.Id, l.Parcel.Code, l.Parcel.StatusId)).FirstOrDefaultAsync(cancellationToken);
        });
    }

    /// <summary>`assertDeliveryPayment` : un « Livré » de prestataire porte le montant encaissé, exactement le dû ; jamais un chèque.</summary>
    private static async Task AssertDeliveryPaymentAsync(IAppDbContext db, ICurrentUser caller, string parcelId, double? collected, CancellationToken cancellationToken)
    {
        if (collected is null) throw BusinessException.Validation("Livré : le montant encaissé est obligatoire.", new { reason = "collected-amount-required" });
        var parcel = await db.Parcels.AsNoTracking().Where(p => p.Id == parcelId).Select(p => new { p.ExpectedPayment, p.CodAmount, p.DeliveryFee, p.FeeIncluded, p.AlreadyPaid })
                         .FirstOrDefaultAsync(cancellationToken)
                     ?? throw NotMine(parcelId);
        if (parcel.ExpectedPayment == ExpectedPayment.CHEQUE) throw BusinessException.Validation("Ce colis est attendu en chèque : il se traite à la main.", new { reason = "cheque-expected" });
        RouteConfirmation.CollectedAmountFor(caller, Billing.CodToCollect(Money.ToCents(parcel.CodAmount), Money.ToCents(parcel.DeliveryFee), parcel.FeeIncluded, parcel.AlreadyPaid), collected);
    }

    private static BusinessException NotMine(string code) => BusinessException.NotFound($"Le colis {code} n’est pas sur une de vos tournées.", new { reason = "parcel-not-mine", code });
}
