using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Routes;

/// <summary>`GET /routes` : par changement de statut, ou les plus récents (`recent=true`).</summary>
public sealed record GetRoutesQuery(RouteListFilter Filter) : IRequest<Paginated<RouteListItemDto>>;

internal sealed class GetRoutesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetRoutesQuery, Paginated<RouteListItemDto>>
{
    public async Task<Paginated<RouteListItemDto>> Handle(GetRoutesQuery request, CancellationToken cancellationToken)
    {
        var paging = request.Filter.Paging;
        var where = RouteRows.Filter(db.Routes.AsNoTracking(), caller, request.Filter);
        var total = await where.CountAsync(cancellationToken);
        var ordered = request.Filter.Recent == "true" ? where.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Code) : where.OrderByDescending(r => r.StatusAt);
        var rows = await RouteRows.Project(ordered.Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize)).ToListAsync(cancellationToken);
        var removed = await RouteRows.RemovedCountsAsync(db, rows.Select(r => r.Id).ToList(), cancellationToken);
        return new Paginated<RouteListItemDto>(rows.Select(r => RouteRows.Fill(new RouteListItemDto(), r, removed.GetValueOrDefault(r.Id))).ToList(), total, paging.Page, paging.PageSize);
    }
}

/// <summary>`GET /routes/stats`.</summary>
public sealed record GetRouteStatsQuery(RouteListFilter Filter) : IRequest<RouteStatsDto>;

internal sealed class GetRouteStatsHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetRouteStatsQuery, RouteStatsDto>
{
    public async Task<RouteStatsDto> Handle(GetRouteStatsQuery request, CancellationToken cancellationToken)
    {
        var where = RouteRows.Filter(db.Routes.AsNoTracking(), caller, request.Filter);
        var total = await where.CountAsync(cancellationToken);
        var open = await where.CountAsync(r => r.Status != RouteStatus.CLOSED, cancellationToken);
        var links = where.SelectMany(r => r.Parcels);
        return new RouteStatsDto(total, open, await links.CountAsync(cancellationToken), await links.CountAsync(l => l.Confirmed, cancellationToken));
    }
}

/// <summary>`GET /routes/export` : classeur « Circuits » (toujours par changement de statut).</summary>
public sealed record ExportRoutesQuery(RouteListFilter Filter) : IRequest<FileContent>;

internal sealed class ExportRoutesHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportRoutesQuery, FileContent>
{
    private static readonly IReadOnlyList<ExportColumn<RouteListItemDto>> Columns =
    [
        new("Circuit", r => r.Code),
        new("Statut", r => DocumentLabels.Enum(r.Status)),
        new("Type", r => DocumentLabels.Enum(r.Kind)),
        new("Livreur", r => r.CourierName),
        new("Ville", r => r.CityName),
        new("Colis", r => r.ParcelCount),
        new("Confirmés", r => r.ConfirmedCount),
        new("Sortie", r => Exports.DateText(r.ExitDate)),
        new("Mis à jour", r => Exports.DateTimeText(r.StatusAt)),
    ];

    public async Task<FileContent> Handle(ExportRoutesQuery request, CancellationToken cancellationToken)
    {
        var where = RouteRows.Filter(db.Routes.AsNoTracking(), caller, request.Filter);
        Exports.AssertWithinLimit(await where.CountAsync(cancellationToken));
        var rows = await RouteRows.Project(where.OrderByDescending(r => r.StatusAt)).ToListAsync(cancellationToken);
        var removed = await RouteRows.RemovedCountsAsync(db, rows.Select(r => r.Id).ToList(), cancellationToken);
        return await Exports.ToXlsxAsync(db, excel, clock, "Circuits", Columns, rows.Select(r => RouteRows.Fill(new RouteListItemDto(), r, removed.GetValueOrDefault(r.Id))).ToList(), cancellationToken);
    }
}

/// <summary>`GET /routes/:code`.</summary>
public sealed record GetRouteQuery(string Code) : IRequest<RouteDetailDto>;

internal sealed class GetRouteHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetRouteQuery, RouteDetailDto>
{
    public Task<RouteDetailDto> Handle(GetRouteQuery request, CancellationToken cancellationToken) => RouteRows.DetailAsync(db, caller, request.Code, cancellationToken);
}

/// <summary>`GET /routes/:code/export` : une ligne par colis (le vendeur ne voit que les siens, le livreur sans frais).</summary>
public sealed record ExportRouteParcelsQuery(string Code) : IRequest<FileContent>;

internal sealed class ExportRouteParcelsHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IClock clock) : IRequestHandler<ExportRouteParcelsQuery, FileContent>
{
    private sealed record Line(string Code, string? ExternalCode, string RecipientName, string RecipientPhone, string City, string? District, string Address, long Cod, long Fee, int StatusId, int Quantity, string Client);

    public async Task<FileContent> Handle(ExportRouteParcelsQuery request, CancellationToken cancellationToken)
    {
        var route = await RouteRows.FindAsync(db, caller, request.Code, cancellationToken);
        var links = db.RouteParcels.AsNoTracking().Where(l => l.RouteId == route.Id);
        if (caller.ClientId is { } clientId) links = links.Where(l => l.Parcel.ClientId == clientId);
        var rows = (await links.OrderBy(l => l.Sequence).ThenBy(l => l.ParcelId)
            .Select(l => new
            {
                l.Parcel.Code, l.Parcel.ExternalCode, l.Parcel.RecipientName, l.Parcel.RecipientPhone, City = l.Parcel.DestinationCity.Name, l.Parcel.DestinationDistrictName,
                DistrictRef = l.Parcel.DestinationDistrict != null ? l.Parcel.DestinationDistrict.Name : null, l.Parcel.DeliveryAddress, l.Parcel.CodAmount, l.Parcel.DeliveryFee, l.Parcel.StatusId,
                l.Parcel.Quantity, Client = l.Parcel.Client.Name,
            }).ToListAsync(cancellationToken))
            .Select(p => new Line(p.Code, p.ExternalCode, p.RecipientName, p.RecipientPhone, p.City, ParcelViews.DistrictLabel(p.DestinationDistrictName, p.DistrictRef), p.DeliveryAddress,
                Money.ToCents(p.CodAmount), Money.ToCents(p.DeliveryFee), p.StatusId, p.Quantity, p.Client)).ToList();
        var columns = new List<ExportColumn<Line>>
        {
            new("Code", r => r.Code, Text: true),
            new("Référence commande", r => r.ExternalCode ?? string.Empty, Text: true),
            new("Destinataire", r => r.RecipientName),
            new("Téléphone", r => r.RecipientPhone, Text: true),
            new("Ville", r => r.City),
            new("Quartier", r => r.District ?? string.Empty),
            new("Adresse", r => r.Address),
            new("Contre-remboursement", r => Exports.Amount(r.Cod)),
            new("Frais de livraison", r => Exports.Amount(r.Fee)),
            new("Statut", r => DocumentLabels.ParcelStatus(r.StatusId)),
            new("Cartons", r => r.Quantity),
            new("Vendeur", r => r.Client),
        };
        if (caller.CourierId is not null) columns.RemoveAll(c => c.Header == "Frais de livraison");
        return await Exports.ToXlsxAsync(db, excel, clock, $"Circuit {route.Code}", columns, rows, cancellationToken);
    }
}

/// <summary>`routeSchema` : un circuit en brouillon, code `CIR-AAMM-NNNN`.</summary>
public sealed record CreateRouteCommand : IRequest<RouteDetailDto>
{
    public static readonly string[] Kinds = ["delivery", "pickup", "return"];

    public string? CityId { get; init; }
    public string? CourierId { get; init; }
    public string? Kind { get; init; } = "delivery";
    public string? ExitDate { get; init => field = Zod.Trim(value); }
}

public sealed class CreateRouteValidator : ZodValidator<CreateRouteCommand>
{
    public CreateRouteValidator()
    {
        RuleFor(x => x.CityId).Required().MinLength(1, "required");
        RuleFor(x => x.CourierId).Required().MinLength(1, "required");
        RuleFor(x => x.Kind).OneOf(CreateRouteCommand.Kinds);
    }
}

internal sealed class CreateRouteHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<CreateRouteCommand, RouteDetailDto>
{
    public async Task<RouteDetailDto> Handle(CreateRouteCommand input, CancellationToken cancellationToken)
    {
        var courier = await db.Couriers.AsNoTracking().Where(c => c.Id == input.CourierId && c.DeletedAt == null).Select(c => new { c.Id, c.CityId, Grid = c.Cities.Count }).FirstOrDefaultAsync(cancellationToken)
                      ?? throw BusinessException.NotFound("Livreur introuvable.");
        var cityId = courier.Grid > 0 ? courier.CityId : input.CityId!;
        if (!await db.Cities.AnyAsync(c => c.Id == cityId && c.DeletedAt == null, cancellationToken)) throw BusinessException.NotFound("Ville introuvable.");
        var origin = await RouteWrites.OriginCityOfAsync(db, caller, cityId, cancellationToken);
        var now = DateTime.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var code = await DocumentCodes.ReserveAsync(db, RouteRows.Prefix, now, cancellationToken);
        db.Routes.Add(new Route
        {
            Code = code, Status = RouteStatus.DRAFT, StatusAt = now, Kind = RouteWrites.KindOf(input.Kind), CityId = cityId, OriginCityId = origin, CourierId = courier.Id,
            ExitDate = string.IsNullOrEmpty(input.ExitDate) ? now : Parcels.Commands.ParcelCreation.ParseDate(input.ExitDate),
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await RouteRows.DetailAsync(db, caller, code, cancellationToken);
    }
}

/// <summary>`routeUpdateSchema` : ville, livreur, type (circuit vide) et date de sortie d'un brouillon.</summary>
public sealed record UpdateRouteCommand : IRequest<RouteDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public string? CityId { get; init; }
    public string? CourierId { get; init; }
    public string? Kind { get; init; }
    public string? ExitDate { get; init => field = Zod.Trim(value); }
}

public sealed class UpdateRouteValidator : ZodValidator<UpdateRouteCommand>
{
    public UpdateRouteValidator()
    {
        RuleFor(x => x.CityId).Must(v => v is null || v.Length >= 1).WithMessage("required");
        RuleFor(x => x.CourierId).Must(v => v is null || v.Length >= 1).WithMessage("required");
        RuleFor(x => x.Kind).OneOf(CreateRouteCommand.Kinds);
    }

    protected override void Refine(UpdateRouteCommand value, Action<string, string> issue)
    {
        if (value.CityId is null && value.CourierId is null && value.Kind is null && value.ExitDate is null) issue(string.Empty, "required");
    }
}

internal sealed class UpdateRouteHandler(IAppDbContext db, ICurrentUser caller, ILogger<UpdateRouteHandler> logger) : IRequestHandler<UpdateRouteCommand, RouteDetailDto>
{
    public async Task<RouteDetailDto> Handle(UpdateRouteCommand input, CancellationToken cancellationToken)
    {
        var route = await RouteRows.FindAsync(db, caller, input.Code, cancellationToken, tracking: true);
        RouteWrites.AssertDraft(route.Status);
        var content = await db.RouteParcels.CountAsync(l => l.RouteId == route.Id, cancellationToken) + await db.RouteNotes.CountAsync(l => l.RouteId == route.Id, cancellationToken);
        RouteKind? kind = input.Kind is null ? null : RouteWrites.KindOf(input.Kind);
        var reshaped = (input.CityId is not null && input.CityId != route.CityId) || (kind is not null && kind != route.Kind);
        if (reshaped && content > 0)
        {
            throw BusinessException.Conflict($"{route.Code} porte des colis ou des bons : retirez-les avant d’en changer la ville ou le type.", new { reason = "route-not-empty" });
        }
        var courierChanged = input.CourierId is not null && input.CourierId != route.CourierId;
        if (courierChanged && content > 0)
        {
            throw BusinessException.Conflict($"{route.Code} porte des colis ou des bons : retirez-les avant d’en changer le livreur.", new { reason = "route-not-empty" });
        }
        if (input.CityId is not null && !await db.Cities.AnyAsync(c => c.Id == input.CityId && c.DeletedAt == null, cancellationToken)) throw BusinessException.NotFound("Ville introuvable.");
        var courier = input.CourierId is null ? null
            : await db.Couriers.AsNoTracking().Where(c => c.Id == input.CourierId && c.DeletedAt == null).Select(c => new { c.Id, c.CityId, Grid = c.Cities.Count }).FirstOrDefaultAsync(cancellationToken)
              ?? throw BusinessException.NotFound("Livreur introuvable.");
        var cityId = courierChanged && courier is { Grid: > 0 } ? courier.CityId : input.CityId;
        var previousCourier = route.CourierId;
        if (cityId is not null && cityId != route.CityId)
        {
            route.CityId = cityId;
            route.OriginCityId = await RouteWrites.OriginCityOfAsync(db, caller, cityId, cancellationToken);
        }
        if (kind is not null && kind != route.Kind) route.Kind = kind.Value;
        if (courierChanged) route.CourierId = input.CourierId!;
        if (!string.IsNullOrEmpty(input.ExitDate)) route.ExitDate = Parcels.Commands.ParcelCreation.ParseDate(input.ExitDate);
        await db.SaveChangesAsync(cancellationToken);
        if (courierChanged) await RouteWrites.NotifyWithdrawnAsync(db, logger, previousCourier, route.Code, cancellationToken);
        return await RouteRows.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}

/// <summary>`DELETE /routes/:code` : un brouillon vide.</summary>
public sealed record DeleteRouteCommand(string Code) : IRequest<RouteCodeDto>;

public sealed record RouteCodeDto(string Code);

internal sealed class DeleteRouteHandler(IAppDbContext db, ICurrentUser caller, ILogger<DeleteRouteHandler> logger) : IRequestHandler<DeleteRouteCommand, RouteCodeDto>
{
    public async Task<RouteCodeDto> Handle(DeleteRouteCommand request, CancellationToken cancellationToken)
    {
        var route = await RouteRows.FindAsync(db, caller, request.Code, cancellationToken, tracking: true);
        RouteWrites.AssertDraft(route.Status);
        var parcels = await db.RouteParcels.CountAsync(l => l.RouteId == route.Id, cancellationToken);
        var notes = await db.RouteNotes.CountAsync(l => l.RouteId == route.Id, cancellationToken);
        if (parcels + notes > 0)
        {
            throw BusinessException.Conflict($"{route.Code} porte encore {parcels} colis et {notes} bon(s) : retirez-les avant de le supprimer.", new { reason = "route-not-empty", parcels, notes });
        }
        db.Routes.Remove(route);
        await db.SaveChangesAsync(cancellationToken);
        await RouteWrites.NotifyWithdrawnAsync(db, logger, route.CourierId, route.Code, cancellationToken);
        return new RouteCodeDto(route.Code);
    }
}

/// <summary>`routeCandidatesQuerySchema` : la liste entière, ou une page avec ses quartiers et villes.</summary>
public sealed record GetRouteCandidatesQuery(string Code, string? Search, string? District, string? CityId, string? Page, string? PageSize) : IRequest<object>;

internal sealed class GetRouteCandidatesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetRouteCandidatesQuery, object>
{
    public async Task<object> Handle(GetRouteCandidatesQuery request, CancellationToken cancellationToken)
    {
        var rows = await RouteWrites.CandidatesAsync(db, caller, request.Code, request.Search, cancellationToken);
        if (string.IsNullOrEmpty(request.Page)) return rows;
        var cities = rows.GroupBy(r => r.CityId).Select(g => new NamedIdDto(g.Key, g.First().CityName)).OrderBy(c => c.Name, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("fr"), false)).ToList();
        var inCity = RouteWrites.InCity(rows, request.CityId).ToList();
        var districts = new Dictionary<string, string>();
        foreach (var row in inCity)
        {
            var name = row.DestinationDistrictName?.Trim();
            if (!string.IsNullOrEmpty(name)) districts.TryAdd(CityNames.DistrictKey(name), name);
        }
        var page = RouteWrites.Page(RouteWrites.InDistrict(inCity, request.District).ToList(), request.Page, request.PageSize, r => r.HeldBy?.Locked == true);
        return new RouteCandidatePageDto(page.Items, page.Total, page.Page, page.PageSize, page.SelectableTotal,
            districts.Select(d => new NamedKeyDto(d.Key, d.Value)).OrderBy(d => d.Name, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("fr"), false)).ToList(), cities);
    }
}

/// <summary>`GET /routes/:code/note-candidates`.</summary>
public sealed record GetRouteNoteCandidatesQuery(string Code, string? Search, string? Page, string? PageSize) : IRequest<object>;

internal sealed class GetRouteNoteCandidatesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetRouteNoteCandidatesQuery, object>
{
    public async Task<object> Handle(GetRouteNoteCandidatesQuery request, CancellationToken cancellationToken)
    {
        var rows = await RouteWrites.NoteCandidatesAsync(db, caller, request.Code, request.Search, cancellationToken);
        return string.IsNullOrEmpty(request.Page) ? rows : RouteWrites.Page(rows, request.Page, request.PageSize);
    }
}

/// <summary>`routeParcelsSchema` : des colis cochés, ou « tout » (recherche, ville, quartier) moins des exclus.</summary>
public sealed record AddRouteParcelsCommand : ParcelSelection, IRequest<RouteDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public string? District { get; init => field = Zod.Trim(value); }
    public string? CityId { get; init => field = Zod.Trim(value); }
}

public sealed class AddRouteParcelsValidator : ParcelSelectionValidator<AddRouteParcelsCommand>
{
    public AddRouteParcelsValidator()
    {
        RuleFor(x => x.District).MaxLength(120);
        RuleFor(x => x.CityId).MaxLength(64);
    }
}

internal sealed class AddRouteParcelsHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<AddRouteParcelsCommand, RouteDetailDto>
{
    public async Task<RouteDetailDto> Handle(AddRouteParcelsCommand input, CancellationToken cancellationToken)
    {
        var route = await RouteRows.FindAsync(db, caller, input.Code, cancellationToken);
        RouteWrites.AssertDraft(route.Status);
        List<string> ids;
        try
        {
            ids = await NoteRules.ResolveSelectionAsync(input.ParcelIds, input.All, input.Exclude, async _ =>
                RouteWrites.InDistrict(RouteWrites.InCity(await RouteWrites.CandidatesAsync(db, caller, input.Code, input.Search, cancellationToken), input.CityId), input.District)
                    .Where(r => r.HeldBy?.Locked != true).Select(r => r.Id).ToList());
        }
        catch (BusinessException error) when (error.Details?.GetType().GetProperty("reason")?.GetValue(error.Details) as string == "selection-too-large")
        {
            throw BusinessException.Validation($"Plus de {NoteRules.SelectionMax} colis sélectionnés : filtrez par ville ou par quartier.", new { reason = "selection-too-large", limit = NoteRules.SelectionMax });
        }
        await RouteWrites.AddParcelsAsync(db, caller, route, ids, cancellationToken);
        return await RouteRows.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}

/// <summary>`DELETE /routes/:code/parcels/:parcelId` (bureau).</summary>
public sealed record RemoveRouteParcelCommand(string Code, string ParcelId) : IRequest<RouteDetailDto>;

internal sealed class RemoveRouteParcelHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<RemoveRouteParcelCommand, RouteDetailDto>
{
    public async Task<RouteDetailDto> Handle(RemoveRouteParcelCommand request, CancellationToken cancellationToken)
    {
        var route = await RouteRows.FindAsync(db, caller, request.Code, cancellationToken);
        RouteWrites.AssertDraft(route.Status);
        await RouteWrites.RemoveParcelFromDraftAsync(db, caller, route, request.ParcelId, "office", cancellationToken);
        return await RouteRows.DetailAsync(db, caller, request.Code, cancellationToken);
    }
}

/// <summary>`routeNotesSchema` : des bons cochés, ou « tout » moins des exclus.</summary>
public sealed record AddRouteNotesCommand : IRequest<RouteDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public IReadOnlyList<string>? NoteIds { get; init; }
    public bool? All { get; init; }
    public string? Search { get; init => field = Zod.Trim(value); }
    public IReadOnlyList<string>? Exclude { get; init; }
}

public sealed class AddRouteNotesValidator : ZodValidator<AddRouteNotesCommand>
{
    public AddRouteNotesValidator()
    {
        RuleFor(x => x.Search).MaxLength(120);
    }

    protected override void Refine(AddRouteNotesCommand value, Action<string, string> issue)
    {
        if (value.All != true && (value.NoteIds?.Count ?? 0) == 0) issue(ValidationPaths.Raw + "noteIds", "required");
    }
}

internal sealed class AddRouteNotesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<AddRouteNotesCommand, RouteDetailDto>
{
    public async Task<RouteDetailDto> Handle(AddRouteNotesCommand input, CancellationToken cancellationToken)
    {
        var route = await RouteRows.FindAsync(db, caller, input.Code, cancellationToken);
        RouteWrites.AssertDraft(route.Status);
        if (route.Kind != RouteKind.PICKUP) throw BusinessException.Conflict("Seul un circuit de ramassage porte des bons.", new { reason = "route-not-pickup" });
        var ids = await NoteRules.ResolveSelectionAsync(input.NoteIds, input.All, input.Exclude,
            async _ => (await RouteWrites.NoteCandidatesAsync(db, caller, input.Code, input.Search, cancellationToken)).Select(n => n.Id).ToList());
        await RouteWrites.AddNotesAsync(db, route, ids, cancellationToken);
        return await RouteRows.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}

/// <summary>`DELETE /routes/:code/notes/:noteId`.</summary>
public sealed record RemoveRouteNoteCommand(string Code, string NoteId) : IRequest<RouteDetailDto>;

internal sealed class RemoveRouteNoteHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<RemoveRouteNoteCommand, RouteDetailDto>
{
    public async Task<RouteDetailDto> Handle(RemoveRouteNoteCommand request, CancellationToken cancellationToken)
    {
        var route = await RouteRows.FindAsync(db, caller, request.Code, cancellationToken);
        RouteWrites.AssertDraft(route.Status);
        await RouteWrites.RemoveNoteAsync(db, route, request.NoteId, cancellationToken);
        return await RouteRows.DetailAsync(db, caller, request.Code, cancellationToken);
    }
}

/// <summary>`routeNotePickupSchema` : les colis pris chez le vendeur ; `removeUnpicked` retire les autres du bon.</summary>
public sealed record PickupRouteNoteCommand : IRequest<RouteDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    [JsonIgnore]
    public string NoteId { get; init; } = string.Empty;

    public IReadOnlyList<string>? ParcelIds { get; init; }
    public bool? RemoveUnpicked { get; init; }
}

public sealed class PickupRouteNoteValidator : ZodValidator<PickupRouteNoteCommand>
{
    public PickupRouteNoteValidator()
    {
        RuleFor(x => x.ParcelIds).Required("array").Custom((ids, context) =>
        {
            if (ids is null) return;
            if (ids.Count < 1) context.AddFailure(ValidationPaths.Raw + "parcelIds", "required");
            if (ids.Count > NoteRules.SelectionMax) context.AddFailure(ValidationPaths.Raw + "parcelIds", "too-many");
            for (var i = 0; i < ids.Count; i++) if (string.IsNullOrEmpty(ids[i])) context.AddFailure(ValidationPaths.Raw + $"parcelIds.{i}", Zod.TooSmallString(1));
        });
    }
}

internal sealed class PickupRouteNoteHandler(IAppDbContext db, ICurrentUser caller, ILogger<PickupRouteNoteHandler> logger) : IRequestHandler<PickupRouteNoteCommand, RouteDetailDto>
{
    public async Task<RouteDetailDto> Handle(PickupRouteNoteCommand input, CancellationToken cancellationToken)
    {
        var route = await RouteRows.FindAsync(db, caller, input.Code, cancellationToken);
        await RouteWrites.PickupNoteAsync(db, caller, logger, route, input.NoteId, input.ParcelIds!, input.RemoveUnpicked == true, cancellationToken);
        return await RouteRows.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}
