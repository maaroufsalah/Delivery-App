using System.Text.Json;
using System.Threading.Channels;
using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.BulkJobs;

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
