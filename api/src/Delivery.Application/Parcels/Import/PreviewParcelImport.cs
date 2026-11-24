using System.Text.Json;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Persistence;
using Delivery.Application.Common.Validation;
using Delivery.Application.Parcels.Commands;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Parcels.Import;

/// <summary>`POST /parcels/import/preview` : chaque ligne avec ses erreurs, rien n'est créé.</summary>
public sealed record PreviewParcelImportCommand : ImportSource, IRequest<ParcelImportPreviewDto>;

public sealed class PreviewParcelImportValidator : ImportSourceValidator<PreviewParcelImportCommand>;

internal sealed class PreviewParcelImportHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel) : IRequestHandler<PreviewParcelImportCommand, ParcelImportPreviewDto>
{
    public async Task<ParcelImportPreviewDto> Handle(PreviewParcelImportCommand input, CancellationToken cancellationToken)
    {
        var context = await ParcelImports.ContextAsync(db, caller, input.ClientId, cancellationToken);
        var (items, ready) = await ParcelImports.ValidateAsync(db, context, ParcelImports.ReadRows(excel, input.Content, input.Rows), cancellationToken);
        return new ParcelImportPreviewDto(context.ClientId, context.ClientName, context.OriginCityName, items.Count, ready.Count, items.Count - ready.Count, items, ready.Sum(r => r.Fee));
    }
}
