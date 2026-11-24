using System.Text.Json.Serialization;
using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Claims;

/// <summary>`ClaimTypesService.update` : le code reste unique parmi les types vivants.</summary>
public sealed record UpdateClaimTypeCommand : ClaimTypeInput, IRequest<ClaimTypeDto>
{
    [JsonIgnore]
    public string Id { get; init; } = string.Empty;
}

public sealed class UpdateClaimTypeValidator : ClaimTypeInputValidator<UpdateClaimTypeCommand>;

internal sealed class UpdateClaimTypeHandler(IAppDbContext db) : IRequestHandler<UpdateClaimTypeCommand, ClaimTypeDto>
{
    public async Task<ClaimTypeDto> Handle(UpdateClaimTypeCommand input, CancellationToken cancellationToken)
    {
        var row = await db.ClaimTypes.FirstOrDefaultAsync(t => t.Id == input.Id && t.DeletedAt == null, cancellationToken)
                  ?? throw BusinessException.NotFound("Type de réclamation introuvable.");
        if (await db.ClaimTypes.AnyAsync(t => t.Code == input.Code && t.DeletedAt == null && t.Id != input.Id, cancellationToken))
        {
            throw BusinessException.Conflict($"Le code « {input.Code} » est déjà pris.", new { field = "code" });
        }
        row.Code = input.Code!;
        input.ApplyTo(row);
        await db.SaveChangesAsync(cancellationToken);
        return ClaimTypeFields.ToDto(row);
    }
}
