namespace Delivery.Api.Auth;

/// <summary>`@Public()` : route ouverte sans session.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class PublicAttribute : Attribute;

/// <summary>`@AllowPasswordChange()` : ouverte à un compte au mot de passe provisoire.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowPasswordChangeAttribute : Attribute;

/// <summary>`@RequirePermission(...)` : l'une des permissions suffit (le super-admin passe).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequirePermissionAttribute(params string[] permissions) : Attribute
{
    public IReadOnlyList<string> Permissions { get; } = permissions;
}

/// <summary>Gardes `@UseGuards(...)` : évaluées dans l'ordre, contrôleur puis méthode.</summary>
public enum Guard
{
    NotMerchant,
    Merchant,
    Courier,
    Staff,
    TechnicalView,
    PublicRateLimit,
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class GuardsAttribute(params Guard[] guards) : Attribute
{
    public IReadOnlyList<Guard> Guards { get; } = guards;
}
