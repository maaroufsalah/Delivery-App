using Delivery.Domain.Enums;

namespace Delivery.Application.Users.Dtos;

/// <summary>`UserListItem` de `@delivery/shared/types/accounts.ts`.</summary>
public class UserListItemDto
{
    public string Id { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Username { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? JobTitle { get; set; }

    public List<RoleName> Roles { get; set; } = [];

    public string? AgencyId { get; set; }

    public string? AgencyName { get; set; }

    public List<string> AgencyIds { get; set; } = [];

    public bool IsActive { get; set; }

    public bool MustChangePassword { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public DateTime? InvitedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>`UserDetail`.</summary>
public sealed class UserDetailDto : UserListItemDto
{
    [System.Text.Json.Serialization.JsonPropertyOrder(1)]
    public string? NationalId { get; set; }

    [System.Text.Json.Serialization.JsonPropertyOrder(1)]
    public string? ClientId { get; set; }

    [System.Text.Json.Serialization.JsonPropertyOrder(1)]
    public string? CourierId { get; set; }
}

/// <summary>`UserInvitation` : le compte et son mot de passe provisoire, montré une fois.</summary>
public sealed record UserInvitationDto(UserListItemDto User, string TemporaryPassword);

public sealed record OkResult(bool Ok);

public sealed record IdResult(string Id);
