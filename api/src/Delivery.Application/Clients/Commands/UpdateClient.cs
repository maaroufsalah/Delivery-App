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

/// <summary>`PATCH /clients/:id` : seuls les champs présents changent ; `agencyId` vide détache l'agence.</summary>
public sealed record UpdateClientCommand(string Id, JsonNode? Body) : IRequest<ClientDetailDto>;

public sealed class UpdateClientValidator : ZodValidator<UpdateClientCommand>
{
    public UpdateClientValidator() => RuleFor(x => x).Custom((value, context) =>
    {
        var issues = new List<(string Path, string Message)>();
        ClientFields.Parse(value.Body, partial: true, issues);
        foreach (var (path, message) in issues) context.AddFailure("@" + path, message);
    });
}

internal sealed class UpdateClientHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<UpdateClientCommand, ClientDetailDto>
{
    public async Task<ClientDetailDto> Handle(UpdateClientCommand request, CancellationToken cancellationToken)
    {
        var client = await ClientRows.FindAsync(db, request.Id, cancellationToken);
        var input = ClientFields.Parse(request.Body, partial: true, []);
        if (input["cityId"]?.GetValue<string>() is { } cityId) await ClientRows.AssertCityExistsAsync(db, cityId, cancellationToken);
        if (Zod.EmptyToNull(input["agencyId"]?.GetValue<string>()) is { } agencyId) await ClientRows.AssertAgencyExistsAsync(db, agencyId, cancellationToken);
        ClientFields.Apply(client, input);
        await db.SaveChangesAsync(cancellationToken);
        return await ClientRows.DetailAsync(db, caller, mapper, request.Id, cancellationToken);
    }
}
