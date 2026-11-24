using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Public;

internal static class PublicCompany
{
    /// <summary>`currentCompany` : un espace suspendu est « inconnu » vu de l'extérieur.</summary>
    public static async Task<Company> CurrentAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        var company = await db.Companies.AsNoTracking().OrderBy(c => c.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        if (company is null || company.Status == CompanyStatus.SUSPENDED) throw BusinessException.NotFound("Espace inconnu.");
        return company;
    }
}
