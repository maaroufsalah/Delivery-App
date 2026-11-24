using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Application.Parcels.Labels;
using Delivery.Domain.Common;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Routes;

/// <summary>`GET /routes/:code/labels` (`labelQuerySchema`) : les étiquettes des colis du circuit.</summary>
public sealed record GetRouteLabelsQuery(string Code, string? Format) : IRequest<FileContent>;

public sealed class GetRouteLabelsValidator : ZodValidator<GetRouteLabelsQuery>
{
    public GetRouteLabelsValidator()
    {
        RuleFor(x => x.Format).OneOf(ParcelLabels.Formats);
    }
}

internal sealed class GetRouteLabelsHandler(IAppDbContext db, ICurrentUser caller, IPdfService pdf, IFileStorage storage) : IRequestHandler<GetRouteLabelsQuery, FileContent>
{
    public async Task<FileContent> Handle(GetRouteLabelsQuery request, CancellationToken cancellationToken)
    {
        // Comme Speedy : le livreur n'atteint que ses circuits, sans la portée d'agence de la fiche.
        var route = await db.Routes.AsNoTracking().Where(r => r.Code == request.Code && (caller.CourierId == null || r.CourierId == caller.CourierId))
                        .Select(r => new { r.Id, r.Code }).FirstOrDefaultAsync(cancellationToken)
                    ?? throw BusinessException.NotFound($"Circuit introuvable : {request.Code}");
        var parcels = await ParcelLabels.LoadAsync(ParcelFilters.Scoped(db.Parcels.AsNoTracking(), caller).Where(p => p.RouteLinks.Any(l => l.RouteId == route.Id)).OrderBy(p => p.Code),
            cancellationToken);
        var sheet = await ParcelLabels.SheetAsync(db, storage, parcels, request.Format ?? LabelSheet.Format10X10, cancellationToken);
        return ParcelLabels.Pdf(pdf.Render(sheet), $"{route.Code}-etiquettes.pdf");
    }
}
