using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.Parcels.Commands;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.DeliveryNotes;

/// <summary>`selectionByCriteria` + `optionalIds` : des colis cochés, ou « tout » (recherche) moins des exclus.</summary>
public abstract record ParcelSelection
{
    public IReadOnlyList<string>? ParcelIds { get; init; }
    public bool? All { get; init; }
    public string? Search { get; init => field = Zod.Trim(value); }
    public IReadOnlyList<string>? Exclude { get; init; }
}

public abstract class ParcelSelectionValidator<T> : ZodValidator<T>
    where T : ParcelSelection
{
    protected ParcelSelectionValidator()
    {
        RuleFor(x => x.ParcelIds).Custom((ids, context) => Ids(ids, "parcelIds", context));
        RuleFor(x => x.Exclude).Custom((ids, context) => Ids(ids, "exclude", context));
        RuleFor(x => x.Search).MaxLength(120);
    }

    private static void Ids(IReadOnlyList<string>? ids, string path, ValidationContext<T> context)
    {
        if (ids is null) return;
        for (var i = 0; i < ids.Count; i++) if (string.IsNullOrEmpty(ids[i])) context.AddFailure(ValidationPaths.Raw + $"{path}.{i}", Zod.TooSmallString(1));
        if (ids.Count > NoteRules.SelectionMax) context.AddFailure(ValidationPaths.Raw + path, "too-many");
    }

    protected override void Refine(T value, Action<string, string> issue)
    {
        if (value.All != true && (value.ParcelIds?.Count ?? 0) == 0) issue(ValidationPaths.Raw + "parcelIds", "required");
    }
}
