using AutoMapper;
using AutoMapper.QueryableExtensions;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Users.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Users.Queries;

/// <summary>`GET /users/export` : la liste courante en classeur « Comptes ».</summary>
public sealed record ExportUsersQuery(GetUsersQuery Filter) : IRequest<FileContent>;

internal sealed class ExportUsersHandler(IAppDbContext db, IMapper mapper, IExcelService excel, IClock clock, ICurrentUser caller) : IRequestHandler<ExportUsersQuery, FileContent>
{
    private static readonly IReadOnlyList<ExportColumn<UserListItemDto>> Columns =
    [
        new("Nom", r => $"{r.FirstName} {r.LastName}"),
        new("E-mail", r => r.Email ?? string.Empty),
        new("Nom d’utilisateur", r => r.Username ?? string.Empty),
        new("Téléphone", r => r.Phone ?? string.Empty),
        new("Fonction", r => r.JobTitle ?? string.Empty),
        new("Rôles", r => string.Join(", ", r.Roles)),
        new("Agence", r => r.AgencyName ?? string.Empty),
        new("Dernière connexion", r => Exports.DateTimeText(r.LastLoginAt)),
        new("Mot de passe à changer", r => Exports.Bool(r.MustChangePassword)),
        new("Actif", r => Exports.Bool(r.IsActive)),
    ];

    public async Task<FileContent> Handle(ExportUsersQuery request, CancellationToken cancellationToken)
    {
        var where = GetUsersHandler.Filter(db.Users.AsNoTracking(), request.Filter);
        var ordered = Sorting.OrderByKey(where, request.Filter.Query, GetUsersHandler.Sorts, "lastName");
        Exports.AssertWithinLimit(await where.CountAsync(cancellationToken));
        var rows = await ordered.ProjectTo<UserListItemDto>(mapper.ConfigurationProvider).ToListAsync(cancellationToken);
        return await Exports.ToXlsxAsync(db, excel, clock, caller, "Comptes", Columns, rows, cancellationToken);
    }
}
