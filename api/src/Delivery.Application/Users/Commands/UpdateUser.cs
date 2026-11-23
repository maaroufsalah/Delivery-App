using AutoMapper;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Application.Users.Dtos;
using Delivery.Application.Users.Queries;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Users.Commands;

/// <summary>`PATCH /users/:id` : identité, rôles (remplacés) et agences (remplacées).</summary>
public sealed record UpdateUserCommand : IRequest<UserDetailDto>, IUserFields
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Id { get; init; } = string.Empty;

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
}

public sealed class UpdateUserValidator : ZodValidator<UpdateUserCommand>
{
    public UpdateUserValidator() => UserFields.Apply(this);

    protected override void Refine(UpdateUserCommand value, Action<string, string> issue) => UserFields.RequireLoginIdentity(value, issue);
}

internal sealed class UpdateUserHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<UpdateUserCommand, UserDetailDto>
{
    public async Task<UserDetailDto> Handle(UpdateUserCommand input, CancellationToken cancellationToken)
    {
        var existing = await db.Users
            .Include(u => u.Roles)
            .Include(u => u.Agencies)
            .Include(u => u.Client)
            .Include(u => u.Courier)
            .FirstOrDefaultAsync(u => u.Id == input.Id && u.DeletedAt == null, cancellationToken)
            ?? throw BusinessException.NotFound("Compte introuvable.");

        var agencyIds = UserFields.AgencyList(input.AgencyId, input.AgencyIds);
        await UserFields.AssertAgenciesExistAsync(db, agencyIds, cancellationToken);
        if (existing.Id == caller.UserId && existing.HasRole(RoleName.SUPER_ADMIN) && !input.Roles!.Contains("SUPER_ADMIN"))
        {
            throw BusinessException.Validation("Un super-administrateur ne peut pas retirer son propre rôle.", new { field = "roles" });
        }
        if (existing.Courier is not null) UserFields.AssertFicheRoles(input.Roles!, RoleName.COURIER);
        if (existing.Client is not null) UserFields.AssertFicheRoles(input.Roles!, RoleName.MERCHANT);
        await UserFields.AssertEmailFreeAsync(db, input.Email, input.Id, cancellationToken);
        await UserFields.AssertUsernameFreeAsync(db, input.Username, input.Id, cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        existing.Email = input.Email;
        existing.Username = input.Username;
        existing.FirstName = input.FirstName!;
        existing.LastName = input.LastName!;
        existing.Phone = Zod.EmptyToNull(input.Phone);
        existing.JobTitle = Zod.EmptyToNull(input.JobTitle);
        existing.NationalId = Zod.EmptyToNull(input.NationalId);
        existing.AgencyId = Zod.EmptyToNull(input.AgencyId);
        existing.Roles.Clear();
        existing.Agencies.Clear();
        await db.SaveChangesAsync(cancellationToken);
        UserFields.AssertDistinctRoles(input.Roles!);
        foreach (var role in input.Roles!) existing.Roles.Add(new UserRole { UserId = existing.Id, Role = Enum.Parse<RoleName>(role) });
        foreach (var agencyId in agencyIds) existing.Agencies.Add(new UserAgency { UserId = existing.Id, AgencyId = agencyId });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetUserHandler.LoadAsync(db, mapper, existing.Id, cancellationToken);
    }
}
