using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Parcels;

/// <summary>`sellerParcel` : le colis du vendeur connecté, encore modifiable.</summary>
internal static class SellerParcels
{
    public static async Task<Parcel> FindAsync(IAppDbContext db, ICurrentUser caller, string code, CancellationToken cancellationToken)
    {
        if (caller.ClientId is null) throw BusinessException.Forbidden("Ce geste est réservé au vendeur du colis.");
        var parcel = await ParcelFilters.Scoped(db.Parcels, caller).Include(p => p.Client).FirstOrDefaultAsync(p => p.Code == code, cancellationToken)
                     ?? throw BusinessException.NotFound($"Colis introuvable : {code}");
        if (parcel.Kind != ParcelKind.STANDARD || !Domain.Parcels.StatusCatalog.IsEditableBySeller(parcel.StatusId))
        {
            throw BusinessException.Validation($"Le colis {parcel.Code} est déjà ramassé : il ne se modifie plus et ne se supprime plus.",
                new { reason = "parcel-picked-up", code = parcel.Code });
        }
        return parcel;
    }
}
