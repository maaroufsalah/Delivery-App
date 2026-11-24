using System.Text.Json;
using System.Threading.Channels;
using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.BulkJobs;

public static class BulkJobKinds
{
    public const string RouteOutcomes = "route-outcomes";
    public const string NoteValidate = "note-validate";
    public const string ParcelTransfer = "parcel-transfer";
    public const string RouteDispatch = "route-dispatch";
    public const int ChunkSize = 250;
}

/// <summary>`BulkJobFailure` : un colis refusé (409/422), la tâche continue.</summary>
public sealed record BulkJobFailureDto(string ParcelId, string? Code, string Reason);

/// <summary>`BulkJob`.</summary>
public sealed record BulkJobDto(string Id, string Kind, string Target, string Status, int Total, int Processed, IReadOnlyList<BulkJobFailureDto> Failures,
    JsonElement? Result, string? Error, DateTime CreatedAt, DateTime? FinishedAt)
{
    public static BulkJobDto From(BulkJob row) => new(row.Id, row.Kind, row.Target, Money.ToWire(row.Status), row.Total, row.Processed,
        JsonSerializer.Deserialize<List<BulkJobFailureDto>>(string.IsNullOrEmpty(row.Failures) ? "[]" : row.Failures, JsonSerializerOptions.Web) ?? [],
        string.IsNullOrEmpty(row.Result) ? null : JsonSerializer.Deserialize<JsonElement>(row.Result), row.Error, row.CreatedAt, row.FinishedAt);
}

/// <summary>`BulkJobAccepted` : la réponse 202 d'un geste parti en tâche de masse.</summary>
public sealed record BulkJobAcceptedDto(BulkJobDto Job);

/// <summary>L'identité du demandeur, rejouée par la tâche (`scope` de Speedy).</summary>
public sealed record CallerSnapshot(string UserId, string? CompanyId, string Email, IReadOnlyList<RoleName> Roles, string? AgencyId, string? ClientId, string? CourierId);

/// <summary>La file en mémoire des tâches de masse ; reprise au démarrage depuis la base.</summary>
public sealed class BulkJobQueue
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(string jobId) => _channel.Writer.TryWrite(jobId);

    public IAsyncEnumerable<string> ReadAllAsync(CancellationToken cancellationToken) => _channel.Reader.ReadAllAsync(cancellationToken);
}

/// <summary>`BulkJobsService` : création verrouillée par objet, une seule tâche active à la fois.</summary>
public static class BulkJobWrites
{
    private static readonly BulkJobStatus[] Active = [BulkJobStatus.PENDING, BulkJobStatus.RUNNING];

    public static async Task AssertIdleAsync(IAppDbContext db, string target, CancellationToken cancellationToken)
    {
        var active = await db.BulkJobs.AsNoTracking().Where(j => j.Target == target && Active.Contains(j.Status)).Select(j => j.Id).FirstOrDefaultAsync(cancellationToken);
        if (active is not null) throw BusinessException.Conflict("Une tâche de masse est en cours sur cet objet.", new { reason = "bulk-job-running", jobId = active });
    }

    public static async Task<BulkJobDto> CreateAsync(IAppDbContext db, ICurrentUser caller, BulkJobQueue queue, string kind, string target, IDictionary<string, object?> input,
        IReadOnlyList<string> parcelIds, CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object?>(input) { ["parcelIds"] = parcelIds };
        var snapshot = new CallerSnapshot(caller.UserId, caller.CompanyId, caller.Email, caller.Roles, caller.AgencyId, caller.ClientId, caller.CourierId);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var lockKey = $"bulk-job:{target}";
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext({lockKey}))", cancellationToken);
        await AssertIdleAsync(db, target, cancellationToken);
        var row = new BulkJob
        {
            RequestedById = caller.UserId,
            Kind = kind,
            Target = target,
            Input = JsonSerializer.Serialize(payload, JsonSerializerOptions.Web),
            Scope = JsonSerializer.Serialize(snapshot, JsonSerializerOptions.Web),
            Actor = JsonSerializer.Serialize(new { userId = caller.UserId, userEmail = caller.Email, role = caller.Roles.FirstOrDefault().ToString(), ip = caller.IpAddress, userAgent = caller.UserAgent }, JsonSerializerOptions.Web),
            Total = parcelIds.Count,
            DoneIds = "[]",
            Failures = "[]",
        };
        db.BulkJobs.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        queue.Enqueue(row.Id);
        return BulkJobDto.From(row);
    }

    /// <summary>`visible` : le vendeur et le livreur ne voient que leurs tâches.</summary>
    public static IQueryable<BulkJob> Visible(IQueryable<BulkJob> jobs, ICurrentUser caller) =>
        caller.ClientId is not null || caller.CourierId is not null ? jobs.Where(j => j.RequestedById == caller.UserId) : jobs;
}

/// <summary>`GET /bulk-jobs?target=` : la dernière tâche de cet objet, ou `null`.</summary>
public sealed record GetLatestBulkJobQuery(string? Target) : IRequest<BulkJobDto?>;

internal sealed class GetLatestBulkJobHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetLatestBulkJobQuery, BulkJobDto?>
{
    public async Task<BulkJobDto?> Handle(GetLatestBulkJobQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Target)) return null;
        var target = request.Target.Length > 64 ? request.Target[..64] : request.Target;
        var row = await BulkJobWrites.Visible(db.BulkJobs.AsNoTracking(), caller).Where(j => j.Target == target).OrderByDescending(j => j.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : BulkJobDto.From(row);
    }
}

/// <summary>`GET /bulk-jobs/:id`.</summary>
public sealed record GetBulkJobQuery(string Id) : IRequest<BulkJobDto>;

internal sealed class GetBulkJobHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetBulkJobQuery, BulkJobDto>
{
    public async Task<BulkJobDto> Handle(GetBulkJobQuery request, CancellationToken cancellationToken) =>
        BulkJobDto.From(await BulkJobWrites.Visible(db.BulkJobs.AsNoTracking(), caller).FirstOrDefaultAsync(j => j.Id == request.Id, cancellationToken)
                        ?? throw BusinessException.NotFound("Tâche introuvable."));
}

/// <summary>`POST /bulk-jobs/:id/retry` : une tâche arrêtée reprend où elle s'était arrêtée.</summary>
public sealed record RetryBulkJobCommand(string Id) : IRequest<BulkJobDto>;

internal sealed class RetryBulkJobHandler(IAppDbContext db, ICurrentUser caller, BulkJobQueue queue) : IRequestHandler<RetryBulkJobCommand, BulkJobDto>
{
    public async Task<BulkJobDto> Handle(RetryBulkJobCommand request, CancellationToken cancellationToken)
    {
        var row = await BulkJobWrites.Visible(db.BulkJobs, caller).FirstOrDefaultAsync(j => j.Id == request.Id, cancellationToken) ?? throw BusinessException.NotFound("Tâche introuvable.");
        if (row.Status != BulkJobStatus.FAILED) throw BusinessException.Conflict("Seule une tâche arrêtée se relance.", new { reason = "bulk-job-not-failed" });
        await BulkJobWrites.AssertIdleAsync(db, row.Target, cancellationToken);
        row.Status = BulkJobStatus.PENDING;
        row.Error = null;
        row.FinishedAt = null;
        await db.SaveChangesAsync(cancellationToken);
        queue.Enqueue(row.Id);
        return BulkJobDto.From(row);
    }
}

/// <summary>Les tâches restées actives, à remettre dans la file au démarrage (`resume`).</summary>
public sealed record GetActiveBulkJobsQuery : IRequest<IReadOnlyList<string>>;

internal sealed class GetActiveBulkJobsHandler(IAppDbContext db) : IRequestHandler<GetActiveBulkJobsQuery, IReadOnlyList<string>>
{
    public async Task<IReadOnlyList<string>> Handle(GetActiveBulkJobsQuery request, CancellationToken cancellationToken) =>
        await db.BulkJobs.AsNoTracking().Where(j => j.Status == BulkJobStatus.PENDING || j.Status == BulkJobStatus.RUNNING).OrderBy(j => j.CreatedAt).Select(j => j.Id).ToListAsync(cancellationToken);
}

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
