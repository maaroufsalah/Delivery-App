using System.Text.Json;
using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Entities;

namespace Delivery.Application.Parcels;

/// <summary>Le journal des champs d'un colis (`fieldChange` avec `entity: 'Parcel'`).</summary>
public static class ParcelJournal
{
    public const string NoteDepartureField = "deliveryNote";
    public const string DeletedPrefix = "Supprimé : ";
    public const string ClientChangePrefix = "Client changé : ";
    public const string SellerDeletionReason = "Supprimé par le vendeur";

    public static FieldChange Record(IAppDbContext db, ICurrentUser caller, string parcelId, string field, string? oldValue, string newValue, string? reason, string authorName, DateTime? at = null)
    {
        var change = new FieldChange
        {
            Entity = "Parcel",
            EntityId = parcelId,
            Field = field,
            OldValue = oldValue,
            NewValue = newValue,
            Reason = reason,
            AuthorId = caller.UserId,
            AuthorName = authorName,
            At = at ?? DateTime.UtcNow,
        };
        db.FieldChanges.Add(change);
        return change;
    }

    /// <summary>`recordNoteDepartures` : une ligne par bon que le colis quitte.</summary>
    public static void NoteDepartures(IAppDbContext db, ICurrentUser caller, string parcelId, IEnumerable<string> noteCodes, string reason, string authorName, DateTime at)
    {
        foreach (var code in noteCodes) Record(db, caller, parcelId, NoteDepartureField, code, string.Empty, reason, authorName, at);
    }
}

/// <summary>`madAmountSchema.optional()` sur un corps JSON : absent, sinon nombre de 0 à 1 000 000.</summary>
public static class OptionalMad
{
    public static bool IsPresent(JsonElement? value) => value is { ValueKind: not JsonValueKind.Undefined };

    public static string? Issue(JsonElement? value)
    {
        if (!IsPresent(value)) return null;
        if (value!.Value.ValueKind != JsonValueKind.Number) return "required";
        var number = value.Value.GetDouble();
        if (number < 0) return "negative-amount";
        if (number > 1_000_000) return "amount-too-large";
        return null;
    }

    public static double? Value(JsonElement? value) => IsPresent(value) && value!.Value.ValueKind == JsonValueKind.Number ? value.Value.GetDouble() : null;
}
