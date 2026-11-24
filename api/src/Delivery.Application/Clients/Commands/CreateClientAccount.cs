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

/// <summary>`POST /clients/:id/account` : crée le compte E-commerçant d'une fiche qui n'en a pas.</summary>
public sealed record CreateClientAccountCommand(string Id, CreateUserCommand Account) : IRequest<UserDetailDto>;

public sealed class CreateClientAccountValidator : ZodValidator<CreateClientAccountCommand>
{
    public CreateClientAccountValidator() => RuleFor(x => x).CustomAsync(async (value, context, cancellationToken) =>
    {
        var result = await new CreateUserValidator().ValidateAsync(value.Account, cancellationToken);
        foreach (var failure in result.Errors) context.AddFailure(failure.PropertyName, failure.ErrorMessage);
    });
}

internal sealed class CreateClientAccountHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IMapper mapper) : IRequestHandler<CreateClientAccountCommand, UserDetailDto>
{
    public async Task<UserDetailDto> Handle(CreateClientAccountCommand request, CancellationToken cancellationToken)
    {
        var client = await ClientRows.FindAsync(db, request.Id, cancellationToken);
        if (client.UserId is not null) throw BusinessException.Conflict("Cette fiche a déjà un compte.", new { field = "email" });
        UserFields.AssertFicheRoles(request.Account.Roles!, RoleName.MERCHANT);
        var created = await CreateUserHandler.CreateAsync(db, caller, clock, mapper, request.Account, cancellationToken);
        client.UserId = created.Id;
        await db.SaveChangesAsync(cancellationToken);
        return created;
    }
}
