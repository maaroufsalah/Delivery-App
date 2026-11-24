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

/// <summary>`POST /clients` : la fiche, validée d'emblée ; agence déduite de la ville si absente.</summary>
public sealed record CreateClientCommand(JsonNode? Body) : IRequest<ClientDetailDto>;

public sealed class CreateClientValidator : ZodValidator<CreateClientCommand>
{
    public CreateClientValidator() => RuleFor(x => x).Custom((value, context) =>
    {
        var issues = new List<(string Path, string Message)>();
        ClientFields.Parse(value.Body, partial: false, issues);
        foreach (var (path, message) in issues) context.AddFailure("@" + path, message);
    });
}

internal sealed class CreateClientHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IMapper mapper) : IRequestHandler<CreateClientCommand, ClientDetailDto>
{
    public async Task<ClientDetailDto> Handle(CreateClientCommand request, CancellationToken cancellationToken)
    {
        var input = ClientFields.Parse(request.Body, partial: false, []);
        var cityId = input["cityId"]!.GetValue<string>();
        var agencyId = Zod.EmptyToNull(input["agencyId"]?.GetValue<string>());
        await ClientRows.AssertCityExistsAsync(db, cityId, cancellationToken);
        if (agencyId is not null) await ClientRows.AssertAgencyExistsAsync(db, agencyId, cancellationToken);
        var client = new Client
        {
            Type = ClientType.MERCHANT,
            BillingType = BillingType.NORMAL,
            IsActive = true,
            ValidatedAt = clock.UtcNow,
            ValidatedById = caller.UserId,
        };
        ClientFields.Apply(client, input);
        client.AgencyId = agencyId ?? await db.AgencyForCityAsync(cityId, cancellationToken);
        db.Clients.Add(client);
        await db.SaveChangesAsync(cancellationToken);
        return await ClientRows.DetailAsync(db, caller, mapper, client.Id, cancellationToken);
    }
}
