using System.Text.Json;
using System.Threading.Channels;
using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.BulkJobs;

/// <summary>`BulkJobFailure` : un colis refusé (409/422), la tâche continue.</summary>
public sealed record BulkJobFailureDto(string ParcelId, string? Code, string Reason);

/// <summary>`BulkJob`.</summary>
public sealed record BulkJobDto(string Id, string Kind, string Target, string Status, int Total, int Processed, IReadOnlyList<BulkJobFailureDto> Failures,
    JsonElement? Result, string? Error, DateTime CreatedAt, DateTime? FinishedAt)
{
    public static BulkJobDto From(BulkJob row) => new(row.Id, row.Kind, row.Target, Money.ToWire(row.Status), row.Total, row.Processed,
        JsonSerializer.Deserialize<List<BulkJobFailureDto>>(string.IsNullOrEmpty(row.Failures) ? "[]" : row.Failures, JsonSerializerOptions.Web) ?? [],
        string.IsNullOrEmpty(row.Result) ? null : JsonSerializer.Deserialize<JsonElement>(row.Result), row.Error, row.CreatedAt, row.FinishedAt);
}

/// <summary>`BulkJobAccepted` : la réponse 202 d'un geste parti en tâche de masse.</summary>
public sealed record BulkJobAcceptedDto(BulkJobDto Job);
