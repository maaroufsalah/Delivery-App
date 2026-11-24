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

/// <summary>`GET /parcels/labels?codes=a,b` : la planche d'une liste de codes (200 au plus).</summary>
public sealed record GetLabelBatchQuery : IRequest<FileContent>
{
    public string? Codes { get; init => field = Zod.Trim(value); }

    public string? Format { get; init; } = LabelSheet.Format10X10;

    [JsonIgnore]
    public IReadOnlyList<string> CodeList => (Codes ?? string.Empty).Split(',').Select(c => c.Trim()).Where(c => c.Length > 0).ToList();
}

public sealed class GetLabelBatchValidator : ZodValidator<GetLabelBatchQuery>
{
    public GetLabelBatchValidator()
    {
        RuleFor(x => x.Codes).Required().MinLength(1);
        RuleFor(x => x.Format).OneOf(ParcelLabels.Formats);
    }

    protected override void Refine(GetLabelBatchQuery value, Action<string, string> issue)
    {
        var count = value.CodeList.Count;
        if (count == 0) issue(ValidationPaths.Raw + "codes", "no-codes");
        if (count > ParcelLabels.BatchMax) issue(ValidationPaths.Raw + "codes", "too-many-codes");
    }
}

internal sealed class GetLabelBatchHandler(IAppDbContext db, ICurrentUser caller, IFileStorage storage, IPdfService pdf) : IRequestHandler<GetLabelBatchQuery, FileContent>
{
    public async Task<FileContent> Handle(GetLabelBatchQuery request, CancellationToken cancellationToken)
    {
        var codes = request.CodeList;
        var parcels = await ParcelLabels.LoadAsync(ParcelFilters.Scoped(db.Parcels.AsNoTracking(), caller).Where(p => codes.Contains(p.Code)), cancellationToken);
        if (parcels.Count == 0) throw BusinessException.NotFound("Aucun de ces colis ne vous est accessible.");
        var sheet = await ParcelLabels.SheetAsync(db, storage, parcels, request.Format ?? LabelSheet.Format10X10, cancellationToken);
        return ParcelLabels.Pdf(ParcelLabels.Render(pdf, sheet).Content, $"etiquettes-{parcels.Count}.pdf");
    }
}
