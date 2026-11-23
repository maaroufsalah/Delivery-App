namespace Delivery.Application.Roles.Dtos;

public sealed record PermissionDto(string Key, string Module, string Label);

/// <summary>`RoleMatrix` : rôles de la société, catalogue, droits accordés par rôle.</summary>
public sealed record RoleMatrixDto(IReadOnlyList<string> Roles, IReadOnlyList<PermissionDto> Permissions, IReadOnlyDictionary<string, IReadOnlyList<string>> Granted);

/// <summary>`UserPermissions` : les droits d'un compte, de son rôle et de ses écarts.</summary>
public sealed record UserPermissionsDto(
    string UserId,
    IReadOnlyList<string> Roles,
    bool Locked,
    IReadOnlyList<PermissionDto> Permissions,
    IReadOnlyList<string> FromRoles,
    IReadOnlyList<string> Grants,
    IReadOnlyList<string> Revokes,
    IReadOnlyList<string> Effective);
