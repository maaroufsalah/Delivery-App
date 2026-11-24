using AutoMapper;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Exchanges.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Exchanges.Queries;

/// <summary>`GET /exchanges/:code`.</summary>
public sealed record GetExchangeQuery(string Code) : IRequest<ExchangeDetailDto>;

internal sealed class GetExchangeHandler(IAppDbContext db, ICurrentUser caller, IMapper mapper) : IRequestHandler<GetExchangeQuery, ExchangeDetailDto>
{
    public Task<ExchangeDetailDto> Handle(GetExchangeQuery request, CancellationToken cancellationToken) =>
        ExchangeRows.DetailAsync(db, caller, mapper, request.Code, cancellationToken);
}
