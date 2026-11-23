namespace Delivery.Application.Common.Models;

/// <summary>`contains` de Prisma (mode `insensitive`) : ILIKE avec jokers échappés.</summary>
public static class Like
{
    public static string Contains(string value) => "%" + Escape(value) + "%";

    public static string StartsWith(string value) => Escape(value) + "%";

    public static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
}
