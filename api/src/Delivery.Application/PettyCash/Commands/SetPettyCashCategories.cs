using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Idempotency;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.PettyCash;

/// <summary>`pettyCashCategoriesSchema` : la liste des catégories, sans doublon (casse ignorée).</summary>
public sealed record SetPettyCashCategoriesCommand : IRequest<PettyCashMetaDto>
{
    public IReadOnlyList<string>? Categories { get; init; }
}

public sealed class SetPettyCashCategoriesValidator : ZodValidator<SetPettyCashCategoriesCommand>
{
    public SetPettyCashCategoriesValidator()
    {
        RuleFor(x => x.Categories).Required("array").Custom((list, context) =>
        {
            if (list is null) return;
            if (list.Count < 1) context.AddFailure(ValidationPaths.Raw + "categories", "required");
            if (list.Count > 30) context.AddFailure(ValidationPaths.Raw + "categories", "Too big: expected array to have <=30 items");
            for (var i = 0; i < list.Count; i++)
            {
                var item = list[i]?.Trim() ?? string.Empty;
                if (item.Length < 1) context.AddFailure(ValidationPaths.Raw + $"categories.{i}", "required");
                else if (item.Length > 60) context.AddFailure(ValidationPaths.Raw + $"categories.{i}", Zod.TooBigString(60));
            }
            if (list.Select(c => (c ?? string.Empty).Trim().ToLowerInvariant()).Distinct().Count() != list.Count) context.AddFailure(ValidationPaths.Raw + "categories", "invalid");
        });
    }
}

internal sealed class SetPettyCashCategoriesHandler(IAppDbContext db, ICurrentUser caller, ISender sender) : IRequestHandler<SetPettyCashCategoriesCommand, PettyCashMetaDto>
{
    public async Task<PettyCashMetaDto> Handle(SetPettyCashCategoriesCommand input, CancellationToken cancellationToken)
    {
        var company = await db.CompanyAsync(cancellationToken);
        await db.SetSettingAsync(company.Id, PettyCashMovements.CategoriesSetting, new JsonArray(input.Categories!.Select(c => (JsonNode?)JsonValue.Create(c.Trim())).ToArray()), cancellationToken);
        _ = caller;
        return await sender.Send(new GetPettyCashMetaQuery(), cancellationToken);
    }
}
