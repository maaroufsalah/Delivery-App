using AutoMapper;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.Users.Dtos;
using Delivery.Application.Users.Queries;
using Delivery.Domain.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Users.Commands;

/// <summary>`PATCH /users/me` : nom, téléphone, e-mail (obligatoire sans nom d'utilisateur).</summary>
public sealed record UpdateOwnProfileCommand : IRequest<UserDetailDto>
{
    public string? FirstName { get; init => field = Zod.Trim(value); }

    public string? LastName { get; init => field = Zod.Trim(value); }

    public string? Phone { get; init => field = Phones.NormalizeOptional(value); }

    public string? Email { get; init => field = UserFields.LoginEmail(value); }
}

public sealed class UpdateOwnProfileValidator : ZodValidator<UpdateOwnProfileCommand>
{
    public UpdateOwnProfileValidator()
    {
        RuleFor(x => x.FirstName).Required().MinLength(2, "too-short").MaxLength(80);
        RuleFor(x => x.LastName).Required().MinLength(2, "too-short").MaxLength(80);
        RuleFor(x => x.Phone).Must(p => string.IsNullOrEmpty(p) || Phones.Pattern.IsMatch(p)).WithMessage(Zod.InvalidInput);
        RuleFor(x => x.Email).Must(e => e is null || Zod.IsEmail(e)).WithMessage(Zod.InvalidInput);
    }
}

internal sealed class UpdateOwnProfileHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<UpdateOwnProfileCommand, UserDetailDto>
{
    public async Task<UserDetailDto> Handle(UpdateOwnProfileCommand input, CancellationToken cancellationToken)
    {
        caller.EnsureScope();
        await UserFields.AssertEmailFreeAsync(db, input.Email, caller.UserId, cancellationToken);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == caller.UserId, cancellationToken)
            ?? throw BusinessException.NotFound("Compte introuvable.");
        if (input.Email is null && user.Username is null)
        {
            throw BusinessException.Validation("L’adresse e-mail est obligatoire.", new { field = "email" });
        }
        user.Email = input.Email;
        user.FirstName = input.FirstName!;
        user.LastName = input.LastName!;
        user.Phone = Zod.EmptyToNull(input.Phone);
        await db.SaveChangesAsync(cancellationToken);
        return await GetUserHandler.LoadAsync(db, mapper, user.Id, cancellationToken);
    }
}
