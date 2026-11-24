using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Providers;

/// <summary>`PATCH /settings/providers/:id/mode` : `LOG_ONLY` (journal) ou `ACTIVE`.</summary>
public sealed record SetProviderModeCommand : IRequest<ProviderTokenListItemDto>
{
    public static readonly string[] Modes = ["LOG_ONLY", "ACTIVE"];

    [JsonIgnore]
    public string Id { get; init; } = string.Empty;

    public string? Mode { get; init; }
}

public sealed class SetProviderModeValidator : ZodValidator<SetProviderModeCommand>
{
    public SetProviderModeValidator()
    {
        RuleFor(x => x.Mode).Required().OneOf(SetProviderModeCommand.Modes);
    }
}

internal sealed class SetProviderModeHandler(IAppDbContext db) : IRequestHandler<SetProviderModeCommand, ProviderTokenListItemDto>
{
    public async Task<ProviderTokenListItemDto> Handle(SetProviderModeCommand input, CancellationToken cancellationToken)
    {
        var row = await ProviderTokens.ActiveAsync(db, input.Id, cancellationToken);
        row.Mode = Enum.Parse<ProviderMode>(input.Mode!);
        await db.SaveChangesAsync(cancellationToken);
        return await ProviderTokens.ItemAsync(db, row.Id, cancellationToken);
    }
}
