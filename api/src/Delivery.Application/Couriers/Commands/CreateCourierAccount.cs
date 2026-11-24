using System.Text.Json.Nodes;
using AutoMapper;
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

namespace Delivery.Application.Couriers.Commands;

/// <summary>`POST /couriers/:id/account` : le compte Livreur de la fiche.</summary>
public sealed record CreateCourierAccountCommand(string Id, CreateUserCommand Account) : IRequest<UserDetailDto>;

public sealed class CreateCourierAccountValidator : ZodValidator<CreateCourierAccountCommand>
{
    public CreateCourierAccountValidator() => RuleFor(x => x).CustomAsync(async (value, context, cancellationToken) =>
    {
        var result = await new CreateUserValidator().ValidateAsync(value.Account, cancellationToken);
        foreach (var failure in result.Errors) context.AddFailure(failure.PropertyName, failure.ErrorMessage);
    });
}

internal sealed class CreateCourierAccountHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IMapper mapper) : IRequestHandler<CreateCourierAccountCommand, UserDetailDto>
{
    public async Task<UserDetailDto> Handle(CreateCourierAccountCommand request, CancellationToken cancellationToken)
    {
        var courier = await CourierRows.FindAsync(db, request.Id, cancellationToken);
        if (courier.UserId is not null) throw BusinessException.Conflict("Cette fiche a déjà un compte.", new { field = "email" });
        UserFields.AssertFicheRoles(request.Account.Roles!, RoleName.COURIER);
        var created = await CreateUserHandler.CreateAsync(db, caller, clock, mapper, request.Account, cancellationToken);
        courier.UserId = created.Id;
        await db.SaveChangesAsync(cancellationToken);
        return created;
    }
}
