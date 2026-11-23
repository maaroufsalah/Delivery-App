using System.Net.Http.Json;
using System.Text.Json;
using Delivery.Application.Common.Security;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;

namespace Delivery.ContractTests.Infrastructure;

/// <summary>Jeux de données des tests de contrat, écrits directement en base.</summary>
public static class TestData
{
    public const string Password = "Secret-123!";

    private static int _sequence;

    public static string Unique(string prefix) => $"{prefix}{Interlocked.Increment(ref _sequence)}{Guid.NewGuid().ToString("N")[..6]}";

    private static string Code3()
    {
        const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        return string.Concat(Enumerable.Range(0, 3).Select(_ => letters[Random.Shared.Next(letters.Length)]));
    }

    public static async Task<(Zone Zone, City City)> CityAsync(ApiFactory factory, string name = "Casablanca")
    {
        var zone = new Zone { Name = Unique("Zone "), BaseRate = 30, SameCityRate = 20 };
        var city = new City { ZoneId = zone.Id, Name = name, Code = Code3() };
        await factory.ExecuteDbAsync(async db =>
        {
            db.Zones.Add(zone);
            db.Cities.Add(city);
            await db.SaveChangesAsync();
        });
        return (zone, city);
    }

    public static async Task<Agency> AgencyAsync(ApiFactory factory, string cityId)
    {
        var agency = new Agency { Name = Unique("Agence "), CityId = cityId };
        await factory.ExecuteDbAsync(async db =>
        {
            db.Agencies.Add(agency);
            await db.SaveChangesAsync();
        });
        return agency;
    }

    public static async Task<User> UserAsync(
        ApiFactory factory, RoleName role, string? agencyId = null, bool mustChangePassword = false, bool active = true, string? email = null, string? username = null)
    {
        var user = new User
        {
            Email = username is null ? email ?? $"{Unique("u")}@wimo.test" : email,
            Username = username,
            PasswordHash = PasswordHasher.Hash(Password),
            FirstName = "Test",
            LastName = Unique("User"),
            AgencyId = agencyId,
            MustChangePassword = mustChangePassword,
            IsActive = active,
            Status = UserStatus.ACTIVE,
            TokensValidFrom = DateTime.UtcNow.AddMinutes(-1),
        };
        user.Roles.Add(new UserRole { UserId = user.Id, Role = role });
        if (agencyId is not null) user.Agencies.Add(new UserAgency { UserId = user.Id, AgencyId = agencyId });
        await factory.ExecuteDbAsync(async db =>
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
        });
        return user;
    }

    public static async Task<Client> MerchantAsync(ApiFactory factory, string cityId)
    {
        var user = await UserAsync(factory, RoleName.MERCHANT);
        var client = new Client { UserId = user.Id, Name = Unique("Boutique "), Phone = "0600000000", Email = user.Email!, CityId = cityId };
        await factory.ExecuteDbAsync(async db =>
        {
            db.Clients.Add(client);
            await db.SaveChangesAsync();
        });
        return client;
    }

    /// <summary>Connexion mobile : rend le jeton d'accès et le corps complet.</summary>
    public static async Task<(string Access, string Refresh, JsonElement Body)> LoginAsync(ApiFactory factory, string login, string password = Password)
    {
        var client = factory.CreateMobileClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { identifier = login, password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("accessToken").GetString()!, body.GetProperty("refreshToken").GetString()!, body);
    }

    public static async Task<HttpClient> ClientForAsync(ApiFactory factory, User user)
    {
        var (access, _, _) = await LoginAsync(factory, user.Email ?? user.Username!);
        var client = factory.CreateClient();
        ApiFactory.UseBearer(client, access);
        return client;
    }

    public static async Task<JsonElement> JsonAsync(this HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
}
