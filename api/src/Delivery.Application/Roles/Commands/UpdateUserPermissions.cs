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
/// `PUT /users/:id/permissions` : l'ensemble effectif voulu ; l'écart au rôle
/// est stocké en accords/retraits.
/// </summary>
public sealed record UpdateUserPermissionsCommand : IRequest<UserPermissionsDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string UserId { get; init; } = string.Empty;

    public List<string>? Effective { get; init; }
}

public sealed class UpdateUserPermissionsValidator : ZodValidator<UpdateUserPermissionsCommand>
{
    private static readonly string[] Keys = PermissionCatalog.All.Select(p => p.Key).ToArray();

    public UpdateUserPermissionsValidator()
    {
        RuleFor(x => x.Effective).Required("array");
        RuleForEach(x => x.Effective).Must(k => Keys.Contains(k)).WithMessage(Zod.InvalidOption(Keys));
    }
}

internal sealed class UpdateUserPermissionsHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<UpdateUserPermissionsCommand, UserPermissionsDto>
{
    public async Task<UserPermissionsDto> Handle(UpdateUserPermissionsCommand input, CancellationToken cancellationToken)
    {
        var roles = await GetUserPermissionsHandler.RolesOfAsync(db, input.UserId, cancellationToken);
        AssertEditable(roles);
        if (input.Effective!.Any(k => k.StartsWith("platform.", StringComparison.Ordinal)))
        {
            throw BusinessException.Forbidden("Les droits de la console plateforme ne s’attribuent pas depuis un espace client.", new { field = "effective" });
        }
        var desired = input.Effective!.ToHashSet(StringComparer.Ordinal);
        var fromRoles = await db.PermissionsForRolesAsync(roles, cancellationToken);
        if (input.UserId == caller.UserId && !desired.Contains("accounts.write"))
        {
            throw BusinessException.Validation("Vous ne pouvez pas vous retirer le droit de gérer les comptes.", new { field = "effective" });
        }

        var wanted = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var permission in PermissionCatalog.All.Where(p => p.Module != "platform"))
        {
            var byRole = fromRoles.Contains(permission.Key);
            var want = desired.Contains(permission.Key);
            if (want && !byRole) wanted[permission.Key] = true;
            if (!want && byRole) wanted[permission.Key] = false;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var existing = await db.UserPermissionOverrides.Where(o => o.UserId == input.UserId).ToListAsync(cancellationToken);
        foreach (var row in existing)
        {
            if (!wanted.TryGetValue(row.PermissionId, out var next)) db.UserPermissionOverrides.Remove(row);
            else if (next != row.Granted)
            {
                row.Granted = next;
                row.CreatedById = caller.UserId;
            }
            wanted.Remove(row.PermissionId);
        }
        foreach (var (permissionId, granted) in wanted)
        {
            db.UserPermissionOverrides.Add(new UserPermissionOverride
            {
                UserId = input.UserId,
                PermissionId = permissionId,
                Granted = granted,
                CreatedById = caller.UserId,
                CreatedAt = clock.UtcNow,
            });
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetUserPermissionsHandler.BuildAsync(db, input.UserId, cancellationToken);
    }

    internal static void AssertEditable(IReadOnlyCollection<RoleName> roles)
    {
        if (roles.Contains(RoleName.SUPER_ADMIN))
        {
            throw BusinessException.Forbidden("Les droits d’un super-administrateur ne se modifient pas compte par compte.");
        }
    }
}
