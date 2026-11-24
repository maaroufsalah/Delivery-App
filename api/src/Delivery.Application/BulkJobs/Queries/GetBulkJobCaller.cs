using System.Text.Json;
using System.Threading.Channels;
using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.BulkJobs;

/// <summary>L'identité rejouée d'une tâche, lue avant de la traiter.</summary>
public sealed record GetBulkJobCallerQuery(string Id) : IRequest<CallerSnapshot?>;

internal sealed class GetBulkJobCallerHandler(IAppDbContext db) : IRequestHandler<GetBulkJobCallerQuery, CallerSnapshot?>
{
    public async Task<CallerSnapshot?> Handle(GetBulkJobCallerQuery request, CancellationToken cancellationToken)
    {
        var scope = await db.BulkJobs.AsNoTracking().Where(j => j.Id == request.Id).Select(j => j.Scope).FirstOrDefaultAsync(cancellationToken);
        return scope is null ? null : JsonSerializer.Deserialize<CallerSnapshot>(scope, JsonSerializerOptions.Web);
    }
}
