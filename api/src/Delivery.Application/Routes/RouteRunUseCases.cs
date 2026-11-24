using System.Text.Json;
using System.Text.Json.Serialization;
using Delivery.Application.BulkJobs;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels.Commands;
using Delivery.Domain.Common;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Routes;

/// <summary>`POST /routes/:code/dispatch` : au-delà de 300 colis avec `x-bulk-jobs: 1`, une tâche de masse.</summary>
public sealed record DispatchRouteCommand(string Code, bool Async) : IRequest<object>;

internal sealed class DispatchRouteHandler(IAppDbContext db, ICurrentUser caller, ILogger<DispatchRouteHandler> logger, BulkJobQueue jobs) : IRequestHandler<DispatchRouteCommand, object>
{
    public async Task<object> Handle(DispatchRouteCommand request, CancellationToken cancellationToken)
    {
        var route = await RouteRows.Scoped(db.Routes.AsNoTracking(), caller).Where(r => r.Code == request.Code)
            .Select(r => new { r.Code, r.Status, Parcels = r.Parcels.Select(l => l.ParcelId).ToList() }).FirstOrDefaultAsync(cancellationToken)
            ?? throw BusinessException.NotFound($"Circuit introuvable : {request.Code}");
        if (route.Status != RouteStatus.DRAFT) throw BusinessException.Conflict("Ce circuit est déjà parti.", new { reason = "route-dispatched" });
        if (route.Parcels.Count == 0) throw BusinessException.Validation("Un circuit sans colis ne part pas.");
        if (request.Async && route.Parcels.Count > NoteRules.BulkAsyncThreshold)
        {
            return new BulkJobAcceptedDto(await BulkJobWrites.CreateAsync(db, caller, jobs, BulkJobKinds.RouteDispatch, route.Code, new Dictionary<string, object?>(), route.Parcels, cancellationToken));
        }
        await BulkJobWrites.AssertIdleAsync(db, route.Code, cancellationToken);
        await RouteDispatch.RunAsync(db, caller, logger, request.Code, new RouteDispatch.Options(), cancellationToken);
        return await RouteRows.DetailAsync(db, caller, request.Code, cancellationToken);
    }
}

/// <summary>`routeConfirmSchema` : le livreur rend compte d'un colis (en main, et son issue).</summary>
public sealed record ConfirmRouteParcelCommand : RouteConfirmBody, IRequest<RouteDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    [JsonIgnore]
    public string ParcelId { get; init; } = string.Empty;

    [JsonIgnore]
    public bool AdminFieldRevision { get; init; }
}

/// <summary>Les champs de `routeConfirmSchema`, partagés par le bureau et le téléphone.</summary>
public abstract record RouteConfirmBody
{
    public bool? Confirmed { get; init; }
    public JsonElement? StatusId { get; init; }
    public string? ScheduledAt { get; init => field = Zod.Trim(value); }
    public string? Comment { get; init => field = Zod.Trim(value); }
    public JsonElement? CollectedAmount { get; init; }
    public JsonElement? Latitude { get; init; }
    public JsonElement? Longitude { get; init; }
    public JsonElement? ChequeAmount { get; init; }
    public string? ChequeNumber { get; init => field = Zod.Trim(value); }
    public string? ChequeBank { get; init => field = Zod.Trim(value); }
    public string? ChequeDate { get; init => field = Zod.Trim(value); }
    public bool? BlCollected { get; init; }
    public string? BlReference { get; init => field = Zod.Trim(value); }

    public RouteOutcome ToOutcome() => new(Confirmed ?? true, RouteInputs.Int(StatusId), ScheduledAt, Comment, RouteInputs.Number(CollectedAmount), RouteInputs.Number(Latitude),
        RouteInputs.Number(Longitude), RouteInputs.Number(ChequeAmount), ChequeNumber, ChequeBank, ChequeDate);
}

public sealed class ConfirmRouteParcelValidator : RouteConfirmBodyValidator<ConfirmRouteParcelCommand>;

public abstract class RouteConfirmBodyValidator<T> : ZodValidator<T> where T : RouteConfirmBody
{
    protected RouteConfirmBodyValidator()
    {
        RuleFor(x => x.Comment).MaxLength(255);
        RuleFor(x => x.ChequeNumber).MaxLength(40);
        RuleFor(x => x.ChequeBank).MaxLength(120);
        RuleFor(x => x.BlReference).MaxLength(60);
    }

    protected override void Refine(T value, Action<string, string> issue)
    {
        RouteInputs.PositiveInt(value.StatusId, "statusId", issue);
        RouteInputs.Amount(value.CollectedAmount, "collectedAmount", issue);
        RouteInputs.Amount(value.ChequeAmount, "chequeAmount", issue);
        RouteInputs.Range(value.Latitude, "latitude", -90, 90, issue);
        RouteInputs.Range(value.Longitude, "longitude", -180, 180, issue);
    }
}

internal sealed class ConfirmRouteParcelHandler(IAppDbContext db, ICurrentUser caller, ILogger<ConfirmRouteParcelHandler> logger) : IRequestHandler<ConfirmRouteParcelCommand, RouteDetailDto>
{
    public async Task<RouteDetailDto> Handle(ConfirmRouteParcelCommand input, CancellationToken cancellationToken)
    {
        await RouteParcelConfirm.RunAsync(db, caller, logger, input.Code, input.ParcelId, input.ToOutcome(), input.AdminFieldRevision, cancellationToken);
        return await RouteRows.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}

/// <summary>`confirmParcel` sans la fiche : partagé avec le statut de terrain de l'administrateur.</summary>
public static class RouteParcelConfirm
{
    public static async Task RunAsync(IAppDbContext db, ICurrentUser caller, ILogger logger, string code, string parcelId, RouteOutcome outcome, bool adminFieldRevision, CancellationToken cancellationToken)
    {
        var route = await RouteConfirmation.RouteAsync(db, caller, code, cancellationToken);
        var link = await db.RouteParcels.Include(l => l.Parcel).FirstOrDefaultAsync(l => l.RouteId == route.Id && l.ParcelId == parcelId, cancellationToken)
                   ?? throw BusinessException.Validation($"Ce colis n'appartient pas au circuit {code}.");
        var (from, clientId, parcelCode) = (link.Parcel.StatusId, link.Parcel.ClientId, link.Parcel.Code);
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await RouteConfirmation.ApplyAsync(db, caller, route, link, outcome, adminFieldRevision, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        db.ChangeTracker.Clear();
        await RouteClosing.AfterOutcomeAsync(db, caller, logger, route, cancellationToken);
        if (outcome.StatusId is { } to && to != from)
        {
            await ParcelStatusChange.NotifyMerchantOfStatusAsync(db, logger, clientId, parcelCode, to, string.IsNullOrEmpty(outcome.ScheduledAt) ? null : outcome.ScheduledAt, cancellationToken);
        }
    }
}

public sealed record RouteBulkConfirmResultDto(int Count, long CashExpected);

/// <summary>`routeBulkConfirmSchema` : la même issue pour les colis cochés, en une transaction (D-146).</summary>
public sealed record ConfirmRouteOutcomesCommand : IRequest<object>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    [JsonIgnore]
    public bool Async { get; init; }

    [JsonIgnore]
    public bool FromJob { get; init; }

    public IReadOnlyList<string>? ParcelIds { get; init; }
    public JsonElement? StatusId { get; init; }
    public string? ScheduledAt { get; init => field = Zod.Trim(value); }
    public string? Comment { get; init => field = Zod.Trim(value); }
}

public sealed class ConfirmRouteOutcomesValidator : ZodValidator<ConfirmRouteOutcomesCommand>
{
    public ConfirmRouteOutcomesValidator()
    {
        RuleFor(x => x.ParcelIds).Required("array").Custom((ids, context) =>
        {
            if (ids is null) return;
            if (ids.Count < 1) context.AddFailure(ValidationPaths.Raw + "parcelIds", "required");
            if (ids.Count > NoteRules.SelectionMax) context.AddFailure(ValidationPaths.Raw + "parcelIds", "too-many");
            for (var i = 0; i < ids.Count; i++) if (string.IsNullOrEmpty(ids[i])) context.AddFailure(ValidationPaths.Raw + $"parcelIds.{i}", Zod.TooSmallString(1));
        });
        RuleFor(x => x.Comment).MaxLength(255);
    }

    protected override void Refine(ConfirmRouteOutcomesCommand value, Action<string, string> issue)
    {
        if (value.StatusId is null) issue(ValidationPaths.Raw + "statusId", Zod.Expected("number"));
        else RouteInputs.PositiveInt(value.StatusId, "statusId", issue);
    }
}

internal sealed class ConfirmRouteOutcomesHandler(IAppDbContext db, ICurrentUser caller, ILogger<ConfirmRouteOutcomesHandler> logger, BulkJobQueue jobs) : IRequestHandler<ConfirmRouteOutcomesCommand, object>
{
    public Task<object> Handle(ConfirmRouteOutcomesCommand input, CancellationToken cancellationToken) => RouteBulkOutcomes.RunAsync(db, caller, logger, jobs, input, cancellationToken);
}

/// <summary>`confirmParcelsInBulk`, partagé avec la tâche de masse `route-outcomes`.</summary>
public static class RouteBulkOutcomes
{
    public static async Task<object> RunAsync(IAppDbContext db, ICurrentUser caller, ILogger logger, BulkJobQueue? jobs, ConfirmRouteOutcomesCommand input, CancellationToken cancellationToken)
    {
        var statusId = RouteInputs.Int(input.StatusId)!.Value;
        var route = await RouteConfirmation.RouteAsync(db, caller, input.Code, cancellationToken);
        var ids = input.ParcelIds!.Distinct().ToList();
        var links = await db.RouteParcels.Include(l => l.Parcel).Where(l => l.RouteId == route.Id && ids.Contains(l.ParcelId)).ToListAsync(cancellationToken);
        if (links.Count != ids.Count)
        {
            throw BusinessException.Validation($"{ids.Count - links.Count} colis coché(s) n’appartiennent plus au circuit {input.Code} : rechargez.",
                new { reason = "parcel-not-on-route", count = ids.Count - links.Count });
        }
        if (statusId == RouteStatusIds.Delivered)
        {
            var byCheque = links.Where(l => l.Parcel.ExpectedPayment == ExpectedPayment.CHEQUE).Select(l => l.Parcel.Code).ToList();
            if (byCheque.Count > 0)
            {
                throw BusinessException.Validation($"{byCheque.Count} colis attendus en chèque : livrez-les un par un ({string.Join(", ", byCheque.Take(5))}).",
                    new { reason = "bulk-cheque-parcels", count = byCheque.Count, codes = byCheque.Take(50).ToList() });
            }
        }
        if (!input.FromJob)
        {
            if (input.Async && jobs is not null && ids.Count > NoteRules.BulkAsyncThreshold)
            {
                return new BulkJobAcceptedDto(await BulkJobWrites.CreateAsync(db, caller, jobs, BulkJobKinds.RouteOutcomes, input.Code,
                    new Dictionary<string, object?> { ["statusId"] = statusId, ["scheduledAt"] = input.ScheduledAt, ["comment"] = input.Comment }, ids, cancellationToken));
            }
            await BulkJobWrites.AssertIdleAsync(db, input.Code, cancellationToken);
        }
        var todo = links.Where(l => !(l.Confirmed && l.Parcel.StatusId == statusId)).ToList();
        var before = todo.Select(l => (l.Parcel.StatusId, l.Parcel.ClientId, l.Parcel.Code)).ToList();
        long cashExpected = 0;
        var outcome = new RouteOutcome(true, statusId, input.ScheduledAt, input.Comment);
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            foreach (var link in todo)
            {
                if (statusId == RouteStatusIds.Delivered)
                {
                    cashExpected += Billing.CodToCollect(Money.ToCents(link.Parcel.CodAmount), Money.ToCents(link.Parcel.DeliveryFee), link.Parcel.FeeIncluded, link.Parcel.AlreadyPaid);
                }
                await RouteConfirmation.ApplyAsync(db, caller, route, link, outcome, false, cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        db.ChangeTracker.Clear();
        await RouteClosing.AfterOutcomeAsync(db, caller, logger, route, cancellationToken);
        foreach (var (from, clientId, code) in before.Where(b => b.StatusId != statusId))
        {
            await ParcelStatusChange.NotifyMerchantOfStatusAsync(db, logger, clientId, code, statusId, string.IsNullOrEmpty(input.ScheduledAt) ? null : input.ScheduledAt, cancellationToken);
        }
        return new RouteBulkConfirmResultDto(todo.Count, cashExpected);
    }
}

/// <summary>`routeCloseSchema` : clôture ; un ramassage exige `removeUnpicked` quand des colis n'ont pas été pris.</summary>
public sealed record CloseRouteCommand : IRequest<RouteDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public bool? RemoveUnpicked { get; init; }
}

public sealed class CloseRouteValidator : ZodValidator<CloseRouteCommand>;

internal sealed class CloseRouteHandler(IAppDbContext db, ICurrentUser caller, ILogger<CloseRouteHandler> logger) : IRequestHandler<CloseRouteCommand, RouteDetailDto>
{
    public async Task<RouteDetailDto> Handle(CloseRouteCommand input, CancellationToken cancellationToken)
    {
        var route = await RouteRows.FindAsync(db, caller, input.Code, cancellationToken);
        await RouteClosing.CloseAsync(db, caller, logger, route, input.RemoveUnpicked == true, null, cancellationToken);
        return await RouteRows.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}

public sealed record AutoClosedRouteDto(string Code, int Delivered, int Returned, int Total);

/// <summary>`POST /routes/auto-close-sweep` : clôt les circuits de livraison ouverts déjà complets.</summary>
public sealed record SweepAutoCloseCommand : IRequest<IReadOnlyList<AutoClosedRouteDto>>;

internal sealed class SweepAutoCloseHandler(IAppDbContext db, ICurrentUser caller, ILogger<SweepAutoCloseHandler> logger) : IRequestHandler<SweepAutoCloseCommand, IReadOnlyList<AutoClosedRouteDto>>
{
    public async Task<IReadOnlyList<AutoClosedRouteDto>> Handle(SweepAutoCloseCommand request, CancellationToken cancellationToken)
    {
        var open = await db.Routes.AsNoTracking().Where(r => r.Kind == RouteKind.DELIVERY && (r.Status == RouteStatus.DISPATCHED || r.Status == RouteStatus.DELIVERING))
            .OrderBy(r => r.CreatedAt).Select(r => new { r.Id, r.Code }).ToListAsync(cancellationToken);
        var closed = new List<AutoClosedRouteDto>();
        foreach (var route in open)
        {
            if (await RouteClosing.AutoCloseDeliveryAsync(db, caller, logger, route.Id, cancellationToken) is { } done) closed.Add(new AutoClosedRouteDto(route.Code, done.Delivered, done.Returned, done.Total));
        }
        return closed;
    }
}

/// <summary>Les nombres du contrat Zod des circuits (`z.number()` : un nombre JSON, pas une chaîne).</summary>
internal static class RouteInputs
{
    public static int? Int(JsonElement? value) => value is { ValueKind: JsonValueKind.Number } v && v.TryGetInt32(out var i) ? i : null;

    public static double? Number(JsonElement? value) => value is { ValueKind: JsonValueKind.Number } v ? v.GetDouble() : null;

    public static void PositiveInt(JsonElement? value, string path, Action<string, string> issue)
    {
        if (value is null) return;
        if (value.Value.ValueKind != JsonValueKind.Number) issue(ValidationPaths.Raw + path, Zod.Expected("number", TypeOf(value.Value)));
        else if (Math.Floor(value.Value.GetDouble()) != value.Value.GetDouble()) issue(ValidationPaths.Raw + path, Zod.ExpectedInt);
        else if (value.Value.GetDouble() <= 0) issue(ValidationPaths.Raw + path, "Too small: expected number to be >0");
    }

    private static string TypeOf(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => "string",
        JsonValueKind.True or JsonValueKind.False => "boolean",
        JsonValueKind.Null => "null",
        JsonValueKind.Array => "array",
        _ => "object",
    };

    public static void Amount(JsonElement? value, string path, Action<string, string> issue)
    {
        if (value is null) return;
        if (value.Value.ValueKind != JsonValueKind.Number) issue(ValidationPaths.Raw + path, Zod.Expected("number", TypeOf(value.Value)));
        else if (value.Value.GetDouble() < 0) issue(ValidationPaths.Raw + path, "negative");
        else if (value.Value.GetDouble() > 1_000_000) issue(ValidationPaths.Raw + path, "amount-too-large");
    }

    public static void Range(JsonElement? value, string path, double min, double max, Action<string, string> issue)
    {
        if (value is null) return;
        if (value.Value.ValueKind != JsonValueKind.Number) issue(ValidationPaths.Raw + path, Zod.Expected("number", TypeOf(value.Value)));
        else if (value.Value.GetDouble() < min) issue(ValidationPaths.Raw + path, Zod.TooSmallNumber((decimal)min));
        else if (value.Value.GetDouble() > max) issue(ValidationPaths.Raw + path, Zod.TooBigNumber((decimal)max));
    }
}
