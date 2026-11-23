using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.Roles.Dtos;
using Delivery.Application.Roles.Queries;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Identity;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Roles.Commands;

/// <summary>
/// `PUT /roles` : remplace la matrice. Le super-administrateur garde tous les
/// droits ; un rôle ne peut pas se retirer `accounts.write`.
/// </summary>
public sealed record UpdateRoleMatrixCommand : IRequest<RoleMatrixDto>
{
    public Dictionary<string, List<string>?>? Granted { get; init; }
}

public sealed class UpdateRoleMatrixValidator : ZodValidator<UpdateRoleMatrixCommand>
{
    private static readonly string[] Roles = Enum.GetNames<RoleName>();
    private static readonly string[] Keys = PermissionCatalog.All.Select(p => p.Key).ToArray();

    public UpdateRoleMatrixValidator()
    {
        RuleFor(x => x.Granted).Required("record");
        RuleFor(x => x).Custom((value, context) =>
        {
            if (value.Granted is null) return;
            // `z.record(z.enum(USER_ROLES), …)` est exhaustif en zod v4.
            foreach (var key in value.Granted.Keys.Where(k => !Roles.Contains(k)))
            {
                context.AddFailure($"granted.{key}", "Invalid key in record");
            }
            foreach (var role in Roles)
            {
                if (!value.Granted.TryGetValue(role, out var keys) || keys is null)
                {
                    context.AddFailure($"granted.{role}", Zod.Expected("array"));
                    continue;
                }
                for (var i = 0; i < keys.Count; i++)
                {
                    if (!Keys.Contains(keys[i])) context.AddFailure($"granted.{role}.{i}", Zod.InvalidOption(Keys));
                }
            }
        });
    }
}

internal sealed class UpdateRoleMatrixHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<UpdateRoleMatrixCommand, RoleMatrixDto>
{
    public async Task<RoleMatrixDto> Handle(UpdateRoleMatrixCommand input, CancellationToken cancellationToken)
    {
        caller.EnsureScope();
        var rows = new List<(RoleName Role, string Key)>();
        foreach (var role in Enum.GetValues<RoleName>())
        {
            if (!input.Granted!.TryGetValue(role.ToString(), out var keys) || keys is null) continue;
            foreach (var key in keys)
            {
                if (!PermissionCatalog.IsPermissionKey(key))
                {
                    throw BusinessException.Validation($"Permission inconnue : {key}", new { field = "granted" });
                }
                if (key.StartsWith("platform.", StringComparison.Ordinal))
                {
                    throw BusinessException.Forbidden("Les droits de la console plateforme ne s’attribuent pas depuis un espace client.", new { field = "granted" });
                }
                rows.Add((role, key));
            }
        }

        var complete = rows.Where(r => r.Role != RoleName.SUPER_ADMIN)
            .Concat(PermissionCatalog.All.Where(p => !p.Key.StartsWith("platform.", StringComparison.Ordinal)).Select(p => (Role: RoleName.SUPER_ADMIN, p.Key)))
            .Distinct()
            .ToList();
        if (!caller.IsSuperAdmin() && !caller.Roles.Any(role => complete.Any(r => r.Role == role && r.Key == "accounts.write")))
        {
            throw BusinessException.Validation("Vous ne pouvez pas retirer à votre propre rôle le droit de gérer les comptes.", new { field = "granted" });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var touched = complete.Select(r => r.Role).Distinct().ToList();
        await db.RolePermissions.Where(r => touched.Contains(r.Role)).ExecuteDeleteAsync(cancellationToken);
        db.RolePermissions.AddRange(complete.Select(r => new RolePermission { Role = r.Role, PermissionId = r.Key }));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetRoleMatrixHandler.BuildAsync(db, cancellationToken);
    }
}
