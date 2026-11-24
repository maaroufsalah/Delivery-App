using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Common.Persistence;

/// <summary>Lecture des violations PostgreSQL sans dépendre du pilote (`SqlState`, `ConstraintName`).</summary>
public static class DbErrors
{
    public static bool IsUniqueViolation(Exception error, string? constraint = null)
    {
        var inner = error is DbUpdateException update ? update.InnerException : error;
        if (inner is null) return false;
        var type = inner.GetType();
        if (type.GetProperty("SqlState")?.GetValue(inner) as string != "23505") return false;
        if (constraint is null) return true;
        var name = type.GetProperty("ConstraintName")?.GetValue(inner) as string;
        return name is not null && name.Contains(constraint, StringComparison.Ordinal);
    }
}
