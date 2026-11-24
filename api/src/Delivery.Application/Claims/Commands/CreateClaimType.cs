using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Claims;

/// <summary>`ClaimTypesService.create` : un code retiré se ressuscite, un code vivant est un conflit.</summary>
public sealed record CreateClaimTypeCommand : ClaimTypeInput, IRequest<ClaimTypeDto>;

public sealed class CreateClaimTypeValidator : ClaimTypeInputValidator<CreateClaimTypeCommand>;

internal sealed class CreateClaimTypeHandler(IAppDbContext db) : IRequestHandler<CreateClaimTypeCommand, ClaimTypeDto>
{
    public async Task<ClaimTypeDto> Handle(CreateClaimTypeCommand input, CancellationToken cancellationToken)
    {
        var row = await db.ClaimTypes.FirstOrDefaultAsync(t => t.Code == input.Code, cancellationToken);
        if (row is { DeletedAt: null }) throw BusinessException.Conflict($"Le code « {input.Code} » est déjà pris.", new { field = "code" });
        if (row is null)
        {
            row = new ClaimType { Code = input.Code! };
            db.ClaimTypes.Add(row);
        }
        row.DeletedAt = null;
        input.ApplyTo(row);
        await db.SaveChangesAsync(cancellationToken);
        return ClaimTypeFields.ToDto(row);
    }
}
