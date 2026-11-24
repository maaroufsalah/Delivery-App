using AutoMapper;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.Users.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Agencies.Commands;

/// <summary>`PATCH /agencies/:id` : la couverture est remplacée ; le siège reste actif.</summary>
public sealed record UpdateAgencyCommand : AgencyFields, IRequest<AgencyListItemDto>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Id { get; init; } = string.Empty;
}

public sealed class UpdateAgencyValidator : AgencyFieldsValidator<UpdateAgencyCommand>;

internal sealed class UpdateAgencyHandler(IAppDbContext db, IMapper mapper) : IRequestHandler<UpdateAgencyCommand, AgencyListItemDto>
{
    public async Task<AgencyListItemDto> Handle(UpdateAgencyCommand input, CancellationToken cancellationToken)
    {
        var agency = await db.Agencies.Include(a => a.CityLinks).FirstOrDefaultAsync(a => a.Id == input.Id && a.DeletedAt == null, cancellationToken)
            ?? throw BusinessException.NotFound("Agence introuvable.");
        await AgencyRows.AssertCityExistsAsync(db, input.CityId!, cancellationToken);
        var covered = await AgencyRows.CheckedCoverageAsync(db, input, cancellationToken);
        if (input.Id == await AgencyRows.HeadquartersIdAsync(db, cancellationToken) && input.IsActive == false)
        {
            throw BusinessException.Validation("Le siège ne peut pas être désactivé.", new { field = "isActive" });
        }
        AgencyRows.Apply(agency, input);
        agency.CityLinks.Clear();
        await db.SaveChangesAsync(cancellationToken);
        foreach (var cityId in covered) agency.CityLinks.Add(new AgencyCity { AgencyId = agency.Id, CityId = cityId });
        await db.SaveChangesAsync(cancellationToken);
        return await AgencyRows.LoadAsync(db, mapper, agency.Id, cancellationToken);
    }
}
