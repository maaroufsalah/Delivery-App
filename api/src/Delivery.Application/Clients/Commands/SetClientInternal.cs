using AutoMapper;
using Delivery.Application.Clients.Dtos;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Clients.Commands;

/// <summary>
/// `PATCH /clients/:id/internal` (`clientInternalSchema`) : un compte vendeur de la société elle-même, exclu du report des
/// factures négatives comme un Client Pro. Administration seule ; journalisé par l'audit (avant / après). Les factures émises ne bougent pas.
/// </summary>
public sealed record SetClientInternalCommand : IRequest<ClientDetailDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Id { get; init; } = string.Empty;

    public bool? IsInternal { get; init; }
}

public sealed class SetClientInternalValidator : ZodValidator<SetClientInternalCommand>
{
    public SetClientInternalValidator()
    {
        RuleFor(x => x.IsInternal).Must(v => v is not null).WithMessage(Zod.Expected("boolean"));
    }
}

internal sealed class SetClientInternalHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<SetClientInternalCommand, ClientDetailDto>
{
    public async Task<ClientDetailDto> Handle(SetClientInternalCommand request, CancellationToken cancellationToken)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == request.Id && c.DeletedAt == null, cancellationToken)
                     ?? throw BusinessException.NotFound("Fiche client introuvable.");
        client.IsInternal = request.IsInternal!.Value;
        await db.SaveChangesAsync(cancellationToken);
        return await ClientRows.DetailAsync(db, caller, mapper, request.Id, cancellationToken);
    }
}
