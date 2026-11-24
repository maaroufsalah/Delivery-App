using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Settings;
using Delivery.Application.Settings.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Enums;
using Delivery.Domain.Pricing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Delivery.Application.Public.Queries;

/// <summary>`GET /public/mobile-config` : version minimale, liens des stores, maintenance.</summary>
public sealed record GetMobileConfigQuery : IRequest<MobileConfigDto>;

public sealed record MobileStoreUrls(string Android, string Ios);

public sealed record MobileConfigDto(string MinVersion, MobileStoreUrls StoreUrls, string Maintenance);

internal sealed class GetMobileConfigHandler(IConfiguration configuration) : IRequestHandler<GetMobileConfigQuery, MobileConfigDto>
{
    public Task<MobileConfigDto> Handle(GetMobileConfigQuery request, CancellationToken cancellationToken)
    {
        string Read(string key) => configuration[key]?.Trim() ?? string.Empty;
        return Task.FromResult(new MobileConfigDto(Read("MOBILE_MIN_VERSION"), new MobileStoreUrls(Read("MOBILE_ANDROID_URL"), Read("MOBILE_IOS_URL")), Read("MOBILE_MAINTENANCE")));
    }
}
