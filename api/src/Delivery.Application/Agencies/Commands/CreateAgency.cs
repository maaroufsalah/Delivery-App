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

/// <summary>`POST /agencies` : une agence et les villes qu'elle couvre.</summary>
public sealed record CreateAgencyCommand : AgencyFields, IRequest<AgencyListItemDto>;

public sealed class CreateAgencyValidator : AgencyFieldsValidator<CreateAgencyCommand>;

internal sealed class CreateAgencyHandler(IAppDbContext db, IMapper mapper) : IRequestHandler<CreateAgencyCommand, AgencyListItemDto>
{
    public async Task<AgencyListItemDto> Handle(CreateAgencyCommand input, CancellationToken cancellationToken)
    {
        await AgencyRows.AssertCityExistsAsync(db, input.CityId!, cancellationToken);
        var covered = await AgencyRows.CheckedCoverageAsync(db, input, cancellationToken);
        var agency = new Agency();
        AgencyRows.Apply(agency, input);
        foreach (var cityId in covered) agency.CityLinks.Add(new AgencyCity { AgencyId = agency.Id, CityId = cityId });
        db.Agencies.Add(agency);
        await db.SaveChangesAsync(cancellationToken);
        return await AgencyRows.LoadAsync(db, mapper, agency.Id, cancellationToken);
    }
}
