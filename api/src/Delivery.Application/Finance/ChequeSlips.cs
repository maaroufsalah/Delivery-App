using System.Globalization;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Returns;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Finance;

/// <summary>`ChequeSlipService` : en-tête des bons de remise (identifiant fiscal Pro, logo assombri).</summary>
internal static class ChequeSlips
{
    public const string Prefix = "BRC";

    public static async Task<(PdfCompany Company, byte[]? Logo)> LetterheadAsync(IAppDbContext db, IFileStorage storage, CancellationToken cancellationToken)
    {
        var company = await PdfCompany.LoadAsync(db, "Bon de remise", cancellationToken);
        var identity = await db.Companies.AsNoTracking().OrderBy(c => c.CreatedAt).Select(c => new { c.Id, c.ProTaxId, c.LogoUrl }).FirstOrDefaultAsync(cancellationToken);
        byte[]? logo = null;
        if (identity is not null && Settings.Logos.KeyOf(identity.Id, identity.LogoUrl) is { } key && !key.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                logo = await storage.GetAsync(key, cancellationToken);
            }
            catch (BusinessException)
            {
                logo = null;
            }
        }
        var taxId = identity?.ProTaxId?.Trim() is { Length: > 0 } pro ? pro : company.TaxId?.Trim() is { Length: > 0 } own ? own : null;
        return (company with { TaxId = taxId }, logo);
    }
}
