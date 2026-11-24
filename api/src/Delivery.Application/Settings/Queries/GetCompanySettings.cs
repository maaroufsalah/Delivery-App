using System.Text.Json;
using System.Text.Json.Nodes;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Settings;
using Delivery.Application.Settings.Dtos;
using MediatR;

namespace Delivery.Application.Settings.Queries;

/// <summary>`GET /settings/company`.</summary>
public sealed record GetCompanySettingsQuery : IRequest<CompanySettingsDto>;

internal sealed class GetCompanySettingsHandler(IAppDbContext db) : IRequestHandler<GetCompanySettingsQuery, CompanySettingsDto>
{
    public async Task<CompanySettingsDto> Handle(GetCompanySettingsQuery request, CancellationToken cancellationToken) =>
        CompanySettingsDto.From(await db.CompanyAsync(cancellationToken));
}
