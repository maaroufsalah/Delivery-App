using System.Text.Json.Nodes;
using AutoMapper;
using Delivery.Application.Clients.Dtos;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.Users;
using Delivery.Application.Users.Commands;
using Delivery.Application.Users.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Clients.Commands;

/// <summary>`PATCH /clients/:id/vat-rate` : taux de TVA d'un Client Pro (points de base).</summary>
public sealed record SetClientVatRateCommand : IRequest<ClientDetailDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Id { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasVatRate { get; private init; }

    public double? VatRate
    {
        get;
        init
        {
            field = value;
            HasVatRate = true;
        }
    }
}

public sealed class SetClientVatRateValidator : ZodValidator<SetClientVatRateCommand>
{
    private static readonly int[] Rates = [2000, 1400, 1000, 700, 0];

    public SetClientVatRateValidator()
    {
        RuleFor(x => x.VatRate).Must((x, v) => x.HasVatRate).WithMessage(Zod.Expected("number"));
        RuleFor(x => x.VatRate).Int().Must(v => v is null || Math.Floor(v.Value) != v.Value || Rates.Contains((int)v.Value)).WithMessage("invalid");
    }
}

internal sealed class SetClientVatRateHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<SetClientVatRateCommand, ClientDetailDto>
{
    public async Task<ClientDetailDto> Handle(SetClientVatRateCommand request, CancellationToken cancellationToken)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == request.Id && c.DeletedAt == null, cancellationToken)
            ?? throw BusinessException.NotFound("Fiche client introuvable.");
        if (!client.IsPro)
        {
            throw BusinessException.Conflict($"{client.Name} n’est pas un Client Pro : la TVA ne s’applique qu’aux Clients Pro.", new { reason = "client-not-pro" });
        }
        client.VatRate = request.VatRate is null ? null : (int)request.VatRate.Value;
        await db.SaveChangesAsync(cancellationToken);
        return await ClientRows.DetailAsync(db, caller, mapper, request.Id, cancellationToken);
    }
}
