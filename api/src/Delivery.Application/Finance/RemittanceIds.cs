using System.Text.Json;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.Returns;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Finance;

internal static class RemittanceIds
{
    public static void Check<T>(IReadOnlyList<string>? ids, ValidationContext<T> context, bool required)
    {
        if (ids is null) return;
        if (required && ids.Count < 1) context.AddFailure(ValidationPaths.Raw + "parcelIds", "required");
        if (ids.Count > NoteRules.SelectionMax) context.AddFailure(ValidationPaths.Raw + "parcelIds", "too-many");
        for (var i = 0; i < ids.Count; i++) if (string.IsNullOrEmpty(ids[i])) context.AddFailure(ValidationPaths.Raw + $"parcelIds.{i}", Zod.TooSmallString(1));
    }
}
