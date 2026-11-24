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

/// <summary>`POST /settings/providers` : un prestataire rattaché au compte d'un livreur, en mode « journal ».</summary>
public sealed record CreateProviderTokenCommand : IRequest<ProviderTokenSecretDto>
{
    public string? Name { get; init => field = Zod.Trim(value); }
    public string? CourierId { get; init; }
}

public sealed class CreateProviderTokenValidator : ZodValidator<CreateProviderTokenCommand>
{
    public CreateProviderTokenValidator()
    {
        RuleFor(x => x.Name).Required().MinLength(1, "required").MaxLength(80);
        RuleFor(x => x.CourierId).Required().MinLength(1, "required");
    }
}

internal sealed class CreateProviderTokenHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<CreateProviderTokenCommand, ProviderTokenSecretDto>
{
    public async Task<ProviderTokenSecretDto> Handle(CreateProviderTokenCommand input, CancellationToken cancellationToken)
    {
        var courier = await db.Couriers.AsNoTracking().Where(c => c.Id == input.CourierId && c.DeletedAt == null).Select(c => new { c.Id, c.UserId }).FirstOrDefaultAsync(cancellationToken)
                      ?? throw BusinessException.NotFound("Livreur introuvable.", new { reason = "courier-not-found" });
        if (courier.UserId is null) throw BusinessException.Validation("Ce livreur n’a pas de compte : créez-le d’abord.", new { reason = "courier-without-account" });
        var token = ProviderTokens.Generate();
        var row = new ProviderToken
        {
            Name = input.Name!,
            CourierId = courier.Id,
            UserId = courier.UserId,
            TokenHash = ProviderTokens.Hash(token),
            TokenPrefix = ProviderTokens.PrefixOf(token),
            Mode = ProviderMode.LOG_ONLY,
            CreatedById = caller.UserId,
        };
        db.ProviderTokens.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return new ProviderTokenSecretDto(await ProviderTokens.ItemAsync(db, row.Id, cancellationToken), token);
    }
}
