using System.Diagnostics;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Identity.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Identity.Queries;

/// <summary>`GET /health` : 200 si la base répond, 503 sinon (corps identique).</summary>
public sealed record GetHealthQuery : IRequest<HealthDto>;

internal sealed class GetHealthHandler(IAppDbContext db) : IRequestHandler<GetHealthQuery, HealthDto>
{
    public async Task<HealthDto> Handle(GetHealthQuery request, CancellationToken cancellationToken)
    {
        var up = true;
        try
        {
            await db.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            up = false;
        }
        var uptime = (long)Math.Round((DateTime.Now - Process.GetCurrentProcess().StartTime).TotalSeconds);
        return new HealthDto(up ? "ok" : "degraded", up ? "up" : "down", uptime);
    }
}
