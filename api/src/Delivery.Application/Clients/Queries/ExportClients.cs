using AutoMapper;
using Delivery.Application.Clients.Dtos;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Clients.Queries;

/// <summary>`GET /clients/export` : classeur « E-commerçants ».</summary>
public sealed record ExportClientsQuery(GetClientsQuery Filter) : IRequest<FileContent>;

internal sealed class ExportClientsHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper, IExcelService excel, IClock clock) : IRequestHandler<ExportClientsQuery, FileContent>
{
    private static readonly IReadOnlyList<ExportColumn<ClientListItemDto>> Columns =
    [
        new("E-commerçant", r => r.Name),
        new("Raison sociale", r => r.LegalName ?? string.Empty),
        new("ICE", r => r.TaxId ?? string.Empty, Text: true),
        new("Téléphone", r => r.Phone, Text: true),
        new("E-mail", r => r.Email),
        new("Ville", r => r.CityName),
        new("Agence", r => r.AgencyName ?? string.Empty),
        new("Colis", r => r.ParcelCount),
        new("Validé le", r => Exports.DateText(r.ValidatedAt)),
        new("Validé par", r => r.ValidatedByName ?? string.Empty),
        new("Actif", r => Exports.Bool(r.IsActive)),
        new("Créé le", r => Exports.DateText(r.CreatedAt)),
    ];

    public async Task<FileContent> Handle(ExportClientsQuery request, CancellationToken cancellationToken)
    {
        var where = GetClientsHandler.Filter(db.Clients.AsNoTracking(), request.Filter, caller);
        Exports.AssertWithinLimit(await where.CountAsync(cancellationToken));
        var rows = await ClientRows.ProjectAsync<ClientListItemDto>(Sorting.OrderByKey(where, request.Filter.Query, ClientRows.Sorts, "name"), mapper, cancellationToken);
        return await Exports.ToXlsxAsync(db, excel, clock, caller, "E-commerçants", Columns, rows, cancellationToken);
    }
}
