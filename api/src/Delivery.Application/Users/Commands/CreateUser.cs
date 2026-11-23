using AutoMapper;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.Users.Dtos;
using Delivery.Application.Users.Queries;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;

namespace Delivery.Application.Users.Commands;

/// <summary>`POST /users` : un compte du personnel (ou d'une fiche), mot de passe posé par l'admin.</summary>
public sealed record CreateUserCommand : IRequest<UserDetailDto>, IUserFields
{
    public string? Email { get; init => field = UserFields.LoginEmail(value); }

    public string? Username { get; init => field = UserFields.OptionalUsername(value); }

    public string? FirstName { get; init => field = Zod.Trim(value); }

    public string? LastName { get; init => field = Zod.Trim(value); }

    public string? Phone { get; init => field = Phones.NormalizeOptional(value); }

    public string? JobTitle { get; init => field = Zod.Trim(value); }

    public string? NationalId { get; init => field = Zod.Trim(value); }

    public IReadOnlyList<string>? Roles { get; init; }

    public string? AgencyId { get; init; }

    public IReadOnlyList<string>? AgencyIds { get; init => field = value ?? []; } = [];

    public string? Password { get; init; }
}

public sealed class CreateUserValidator : ZodValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        UserFields.Apply(this);
        RuleFor(x => x.Password).Required().MinLength(8, "password-too-short").MaxLength(200);
    }

    protected override void Refine(CreateUserCommand value, Action<string, string> issue) => UserFields.RequireLoginIdentity(value, issue);
}

internal sealed class CreateUserHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IMapper mapper) : IRequestHandler<CreateUserCommand, UserDetailDto>
{
    public async Task<UserDetailDto> Handle(CreateUserCommand input, CancellationToken cancellationToken)
    {
        await UserFields.AssertEmailFreeAsync(db, input.Email, null, cancellationToken);
        await UserFields.AssertUsernameFreeAsync(db, input.Username, null, cancellationToken);
        var agencyIds = UserFields.AgencyList(input.AgencyId, input.AgencyIds);
        await UserFields.AssertAgenciesExistAsync(db, agencyIds, cancellationToken);

        var user = new User
        {
            Email = input.Email,
            Username = input.Username,
            PasswordHash = PasswordHasher.Hash(input.Password!),
            FirstName = input.FirstName!,
            LastName = input.LastName!,
            Phone = Zod.EmptyToNull(input.Phone),
            JobTitle = Zod.EmptyToNull(input.JobTitle),
            NationalId = Zod.EmptyToNull(input.NationalId),
            AgencyId = Zod.EmptyToNull(input.AgencyId),
            MustChangePassword = false,
            Status = UserStatus.ACTIVE,
            InvitedAt = clock.UtcNow,
            InvitedById = caller.UserId,
        };
        UserFields.AssertDistinctRoles(input.Roles!);
        foreach (var role in input.Roles!) user.Roles.Add(new UserRole { UserId = user.Id, Role = Enum.Parse<RoleName>(role) });
        foreach (var agencyId in agencyIds) user.Agencies.Add(new UserAgency { UserId = user.Id, AgencyId = agencyId });
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return await GetUserHandler.LoadAsync(db, mapper, user.Id, cancellationToken);
    }
}
