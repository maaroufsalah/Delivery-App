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
        RouteInputs.Number(Longitude), RouteInputs.Number(ChequeAmount), ChequeNumber, ChequeBank, ChequeDate, BlCollected, BlReference);
}

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
