using AutoMapper;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.DeliveryTypes;

/// <summary>`DeliveryTypeListItem` : le multiplicateur en millièmes.</summary>
public sealed record DeliveryTypeListItemDto(string Id, string Name, long MultiplierPerMille, bool IsActive, bool IsDefault, int ParcelCount);

public sealed record DeliveryTypeRow(string Id, string Name, decimal Multiplier, bool IsActive, bool IsDefault, int ParcelCount)
{
    public DeliveryTypeListItemDto ToDto() => new(Id, Name, (long)Math.Floor((Multiplier * 1000m) + 0.5m), IsActive, IsDefault, ParcelCount);
}

public sealed class DeliveryTypesProfile : Profile
{
    public DeliveryTypesProfile()
    {
        CreateProjection<DeliveryType, DeliveryTypeRow>()
            .ForCtorParam(nameof(DeliveryTypeRow.ParcelCount), o => o.MapFrom(s => s.Parcels.Count));
    }
}

/// <summary>`deliveryTypeSchema`.</summary>
public abstract record DeliveryTypeFields
{
    public string? Name { get; init => field = Zod.Trim(value); }

    public double? MultiplierPerMille { get; init; }

    public bool? IsActive { get; init; } = true;

    public bool? IsDefault { get; init; } = false;

    /// <summary>`fromPerMille` : millièmes → facteur à trois décimales.</summary>
    public decimal Multiplier => Math.Round((decimal)Math.Floor(MultiplierPerMille!.Value + 0.5) / 1000m, 3);
}

public abstract class DeliveryTypeFieldsValidator<T> : ZodValidator<T>
    where T : DeliveryTypeFields
{
    protected DeliveryTypeFieldsValidator()
    {
        RuleFor(x => x.Name).Required().MinLength(2, "too-short").MaxLength(80);
        RuleFor(x => x.MultiplierPerMille).Required("number").Int().Min(1, "too-small").Max(10_000, "too-large");
    }
}

internal static class DeliveryTypeRows
{
    public static readonly IReadOnlyDictionary<string, Func<IQueryable<DeliveryType>, bool, IOrderedQueryable<DeliveryType>>> Sorts =
        new Dictionary<string, Func<IQueryable<DeliveryType>, bool, IOrderedQueryable<DeliveryType>>>
        {
            ["name"] = (q, d) => d ? q.OrderByDescending(x => x.Name) : q.OrderBy(x => x.Name),
            ["multiplierPerMille"] = (q, d) => d ? q.OrderByDescending(x => x.Multiplier) : q.OrderBy(x => x.Multiplier),
            ["isActive"] = (q, d) => d ? q.OrderByDescending(x => x.IsActive) : q.OrderBy(x => x.IsActive),
        };

    public static async Task<DeliveryTypeListItemDto> LoadAsync(IAppDbContext db, IMapper mapper, string id, CancellationToken cancellationToken)
    {
        var row = await mapper.ProjectTo<DeliveryTypeRow>(db.DeliveryTypes.AsNoTracking().Where(t => t.Id == id && t.DeletedAt == null)).FirstOrDefaultAsync(cancellationToken)
            ?? throw BusinessException.NotFound("Type de livraison introuvable.");
        return row.ToDto();
    }

    public static async Task AssertNameFreeAsync(IAppDbContext db, string name, string? exceptId, CancellationToken cancellationToken)
    {
        var clash = await db.DeliveryTypes.AnyAsync(t => t.Name.ToLower() == name.ToLower() && t.DeletedAt == null && (exceptId == null || t.Id != exceptId), cancellationToken);
        if (clash) throw BusinessException.Conflict($"« {name} » existe déjà.", new { field = "name" });
    }

    public static Task ClearDefaultAsync(IAppDbContext db, string? exceptId, CancellationToken cancellationToken) =>
        db.DeliveryTypes.Where(t => t.IsDefault && (exceptId == null || t.Id != exceptId))
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.IsDefault, false), cancellationToken);
}
