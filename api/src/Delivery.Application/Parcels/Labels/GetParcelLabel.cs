using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Parcels.Labels;

/// <summary>`GET /parcels/:code/label` : l'étiquette d'un colis.</summary>
public sealed record GetParcelLabelQuery : IRequest<FileContent>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public string? Format { get; init; } = LabelSheet.Format10X10;
}

public sealed class GetParcelLabelValidator : ZodValidator<GetParcelLabelQuery>
{
    public GetParcelLabelValidator()
    {
        RuleFor(x => x.Format).OneOf(ParcelLabels.Formats);
    }
}

internal sealed class GetParcelLabelHandler(IAppDbContext db, ICurrentUser caller, IFileStorage storage, IPdfService pdf) : IRequestHandler<GetParcelLabelQuery, FileContent>
{
    public async Task<FileContent> Handle(GetParcelLabelQuery request, CancellationToken cancellationToken)
    {
        var parcels = await ParcelLabels.LoadAsync(ParcelFilters.Scoped(db.Parcels.AsNoTracking(), caller).Where(p => p.Code == request.Code), cancellationToken);
        if (parcels.Count == 0) throw BusinessException.NotFound($"Colis introuvable : {request.Code}");
        var sheet = await ParcelLabels.SheetAsync(db, storage, parcels, request.Format ?? LabelSheet.Format10X10, cancellationToken);
        return ParcelLabels.Pdf(ParcelLabels.Render(pdf, sheet).Content, $"{request.Code}.pdf");
    }
}
