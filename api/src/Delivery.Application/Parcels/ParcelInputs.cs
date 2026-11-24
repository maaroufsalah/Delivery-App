using System.Text.RegularExpressions;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Parcels;

/// <summary>`recipientPhonesSchema`, `resolveDistrict`, `expectedPaymentFor`, `cartonsFee`.</summary>
public static class ParcelInputs
{
    public const int RecipientPhonesMax = 3;
    public const int PhoneCompleteDigits = 9;
    public const string RecipientPhonesJoin = " / ";
    public const int DistrictNameMax = 120;
    public const int QuantityMax = 999;
    public static readonly string[] DeliveryTargets = ["district", "agency", "relay-point"];
    public static readonly string[] ExpectedPayments = ["cash", "cheque"];

    private static readonly Regex SegmentSplit = new("[/,;]", RegexOptions.Compiled);
    private static readonly Regex PieceSplit = new(@"[\s.]+", RegexOptions.Compiled);

    /// <summary>`splitPhones` : un à trois numéros saisis librement, normalisés et dédoublonnés.</summary>
    public static IReadOnlyList<string> SplitPhones(string input)
    {
        var phones = new List<string>();
        foreach (var segment in SegmentSplit.Split(input))
        {
            var pieces = PieceSplit.Split(segment).Where(p => p.Length > 0).ToList();
            var current = string.Empty;
            for (var i = 0; i < pieces.Count; i++)
            {
                var rest = string.Concat(pieces.Skip(i));
                if (current.Length > 0 && Digits(current) >= PhoneCompleteDigits && Digits(rest) >= PhoneCompleteDigits)
                {
                    phones.Add(current);
                    current = pieces[i];
                }
                else current += pieces[i];
            }
            if (current.Length > 0) phones.Add(current);
        }
        var cleaned = phones.Select(Phones.Normalize).Where(p => p.Length > 0).ToList();
        return cleaned.Where((p, i) => cleaned.IndexOf(p) == i).ToList();
    }

    /// <summary>Problèmes zod de `recipientPhonesSchema` (chemin relatif au champ).</summary>
    public static IEnumerable<(string Path, string Message)> RecipientPhoneIssues(string? value)
    {
        if (value is null) yield break;
        var phones = SplitPhones(value);
        for (var i = 0; i < phones.Count; i++)
        {
            if (!Phones.Pattern.IsMatch(phones[i])) yield return ($".{i}", "invalid-phone");
        }
        if (phones.Count < 1) yield return (string.Empty, "invalid-phone");
        if (phones.Count > RecipientPhonesMax) yield return (string.Empty, "too-many-phones");
    }

    public static string JoinPhones(string value) => string.Join(RecipientPhonesJoin, SplitPhones(value));

    private static int Digits(string text) => text.Count(char.IsAsciiDigit);

    /// <summary>`resolveDistrict` : le nom saisi l'emporte (rapproché du référentiel), sinon l'id connu.</summary>
    public static async Task<(string? DistrictId, string? DistrictName)> ResolveDistrictAsync(IAppDbContext db, string cityId, string? districtId, string? districtName, CancellationToken cancellationToken)
    {
        var typed = districtName?.Trim() ?? string.Empty;
        var id = districtId?.Trim() ?? string.Empty;
        if (typed.Length == 0 && id.Length == 0) return (null, null);
        var districts = await db.Districts.AsNoTracking().Where(d => d.CityId == cityId && d.DeletedAt == null)
            .Select(d => new { d.Id, d.Name, d.IsActive }).ToListAsync(cancellationToken);
        if (typed.Length > 0)
        {
            var key = CityNames.DistrictKey(typed);
            var candidates = districts.Where(d => CityNames.DistrictKey(d.Name) == key).ToList();
            var match = candidates.FirstOrDefault(d => d.IsActive) ?? candidates.FirstOrDefault();
            return match is null ? (null, typed) : (match.Id, match.Name);
        }
        var byId = districts.FirstOrDefault(d => d.Id == id);
        return byId is null ? (null, null) : (byId.Id, byId.Name);
    }

    /// <summary>`expectedPaymentFor` : le chèque est réservé au Client Pro.</summary>
    public static ExpectedPayment? ExpectedPaymentFor(bool isPro, string? requested)
    {
        if (!isPro)
        {
            if (requested == "cheque") throw BusinessException.Conflict("Le paiement par chèque ne se choisit que pour un Client Pro.", new { reason = "client-not-pro" });
            return null;
        }
        return requested == "cheque" ? ExpectedPayment.CHEQUE : ExpectedPayment.CASH;
    }

    /// <summary>`cartonsFee` : le tarif unitaire × le nombre de cartons.</summary>
    public static long CartonsFee(long unitFee, int quantity) => unitFee * Math.Max(quantity, 1);

    /// <summary>`parcelQuantitySchema` (coercition JS) : message ou null.</summary>
    public static string? QuantityIssue(double value)
    {
        if (double.IsNaN(value)) return "required";
        if (double.IsInfinity(value) || Math.Floor(value) != value) return "not-an-integer";
        if (value < 1) return "quantity-min";
        if (value > QuantityMax) return "quantity-max";
        return null;
    }
}
