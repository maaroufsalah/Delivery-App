using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Parcels.Labels;

/// <summary>`GET /parcels/:code/label` : l'étiquette d'un colis.</summary>
public sealed record GetParcelLabelQuery : IRequest<FileContent>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public string? Format { get; init; } = LabelSheet.Format10X10;
}

public sealed class GetParcelLabelValidator : ZodValidator<GetParcelLabelQuery>
{
    public GetParcelLabelValidator()
    {
        RuleFor(x => x.Format).OneOf(ParcelLabels.Formats);
    }
}

internal sealed class GetParcelLabelHandler(IAppDbContext db, ICurrentUser caller, IFileStorage storage, IPdfService pdf) : IRequestHandler<GetParcelLabelQuery, FileContent>
{
    public async Task<FileContent> Handle(GetParcelLabelQuery request, CancellationToken cancellationToken)
    {
        var parcels = await ParcelLabels.LoadAsync(ParcelFilters.Scoped(db.Parcels.AsNoTracking(), caller).Where(p => p.Code == request.Code), cancellationToken);
        if (parcels.Count == 0) throw BusinessException.NotFound($"Colis introuvable : {request.Code}");
        var sheet = await ParcelLabels.SheetAsync(db, storage, parcels, request.Format ?? LabelSheet.Format10X10, cancellationToken);
        return ParcelLabels.Pdf(ParcelLabels.Render(pdf, sheet).Content, $"{request.Code}.pdf");
    }
}

/// <summary>`GET /parcels/labels?codes=a,b` : la planche d'une liste de codes (200 au plus).</summary>
public sealed record GetLabelBatchQuery : IRequest<FileContent>
{
    public string? Codes { get; init => field = Zod.Trim(value); }

    public string? Format { get; init; } = LabelSheet.Format10X10;

    [JsonIgnore]
    public IReadOnlyList<string> CodeList => (Codes ?? string.Empty).Split(',').Select(c => c.Trim()).Where(c => c.Length > 0).ToList();
}

public sealed class GetLabelBatchValidator : ZodValidator<GetLabelBatchQuery>
{
    public GetLabelBatchValidator()
    {
        RuleFor(x => x.Codes).Required().MinLength(1);
        RuleFor(x => x.Format).OneOf(ParcelLabels.Formats);
    }

    protected override void Refine(GetLabelBatchQuery value, Action<string, string> issue)
    {
        var count = value.CodeList.Count;
        if (count == 0) issue(ValidationPaths.Raw + "codes", "no-codes");
        if (count > ParcelLabels.BatchMax) issue(ValidationPaths.Raw + "codes", "too-many-codes");
    }
}

internal sealed class GetLabelBatchHandler(IAppDbContext db, ICurrentUser caller, IFileStorage storage, IPdfService pdf) : IRequestHandler<GetLabelBatchQuery, FileContent>
{
    public async Task<FileContent> Handle(GetLabelBatchQuery request, CancellationToken cancellationToken)
    {
        var codes = request.CodeList;
        var parcels = await ParcelLabels.LoadAsync(ParcelFilters.Scoped(db.Parcels.AsNoTracking(), caller).Where(p => codes.Contains(p.Code)), cancellationToken);
        if (parcels.Count == 0) throw BusinessException.NotFound("Aucun de ces colis ne vous est accessible.");
        var sheet = await ParcelLabels.SheetAsync(db, storage, parcels, request.Format ?? LabelSheet.Format10X10, cancellationToken);
        return ParcelLabels.Pdf(ParcelLabels.Render(pdf, sheet).Content, $"etiquettes-{parcels.Count}.pdf");
    }
}

/// <summary>`labelSelectionSchema` : des codes, ou « tout » (filtres de la liste) moins des exclus.</summary>
public abstract record LabelSelection
{
    public IReadOnlyList<string>? Codes { get; init => field = value?.Select(c => c?.Trim() ?? string.Empty).ToList(); }

    public bool? All { get; init; }

    public IReadOnlyList<string>? Exclude { get; init => field = value?.Select(c => c?.Trim() ?? string.Empty).ToList(); }

    public ParcelListParams? Filters { get; init; }

    public string? Format { get; init; } = LabelSheet.Format10X10;
}

public abstract class LabelSelectionValidator<T> : ZodValidator<T>
    where T : LabelSelection
{
    protected LabelSelectionValidator()
    {
        RuleFor(x => x.Codes).Custom((codes, context) =>
        {
            if (codes is null) return;
            for (var i = 0; i < codes.Count; i++) if (codes[i].Length < 1) context.AddFailure(ValidationPaths.Raw + $"codes.{i}", Zod.TooSmallString(1));
            if (codes.Count > ParcelLabels.SelectionMax) context.AddFailure(ValidationPaths.Raw + "codes", "too-many");
        });
        RuleFor(x => x.Exclude).Custom((codes, context) =>
        {
            if (codes is null) return;
            for (var i = 0; i < codes.Count; i++) if (codes[i].Length < 1) context.AddFailure(ValidationPaths.Raw + $"exclude.{i}", Zod.TooSmallString(1));
            if (codes.Count > ParcelLabels.SelectionMax) context.AddFailure(ValidationPaths.Raw + "exclude", "too-many");
        });
        RuleFor(x => x.Format).OneOf(ParcelLabels.Formats);
    }

    protected override void Refine(T value, Action<string, string> issue)
    {
        if (value.All != true && (value.Codes?.Count ?? 0) == 0) issue(ValidationPaths.Raw + "codes", "required");
    }
}

/// <summary>`labelCodes` : les codes de la sélection, dans l'ordre de la liste.</summary>
public static class LabelSelections
{
    public static async Task<IReadOnlyList<string>> CodesAsync(IAppDbContext db, ICurrentUser caller, IClock clock, LabelSelection input, CancellationToken cancellationToken)
    {
        var filter = input.All == true ? (input.Filters ?? new ParcelListParams()).ToFilter(withPaging: false) : new ParcelListFilter();
        var (where, _) = await ParcelFilters.InPeriodAsync(db, caller, filter, clock, cancellationToken);
        if (input.All == true)
        {
            if (input.Exclude is { Count: > 0 } exclude) where = where.Where(p => !exclude.Contains(p.Code));
        }
        else
        {
            var codes = input.Codes ?? [];
            where = where.Where(p => codes.Contains(p.Code));
        }
        var rows = await where.OrderByDescending(p => p.StatusAt).ThenBy(p => p.Code).Select(p => p.Code).Take(ParcelLabels.SelectionMax + 1).ToListAsync(cancellationToken);
        if (rows.Count > ParcelLabels.SelectionMax)
        {
            throw BusinessException.Validation($"Plus de {ParcelLabels.SelectionMax} colis : affinez les filtres pour imprimer les étiquettes.", new { reason = "selection-too-large", max = ParcelLabels.SelectionMax });
        }
        if (rows.Count == 0) throw BusinessException.NotFound("Aucun de ces colis ne vous est accessible.");
        return rows;
    }
}

/// <summary>`POST /parcels/labels` : la planche d'une sélection, rendue tout de suite (500 au plus).</summary>
public sealed record PrintLabelSelectionCommand : LabelSelection, IRequest<FileContent>;

public sealed class PrintLabelSelectionValidator : LabelSelectionValidator<PrintLabelSelectionCommand>;

internal sealed class PrintLabelSelectionHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IFileStorage storage, IPdfService pdf) : IRequestHandler<PrintLabelSelectionCommand, FileContent>
{
    public async Task<FileContent> Handle(PrintLabelSelectionCommand request, CancellationToken cancellationToken)
    {
        var codes = await LabelSelections.CodesAsync(db, caller, clock, request, cancellationToken);
        if (codes.Count > ParcelLabels.AsyncThreshold)
        {
            throw BusinessException.Validation($"Plus de {ParcelLabels.AsyncThreshold} étiquettes : elles se préparent en arrière-plan.",
                new { reason = "labels-async", count = codes.Count, threshold = ParcelLabels.AsyncThreshold });
        }
        var parcels = ParcelLabels.InOrder(await ParcelLabels.LoadAsync(ParcelFilters.Scoped(db.Parcels.AsNoTracking(), caller).Where(p => codes.Contains(p.Code)), cancellationToken), codes);
        if (parcels.Count == 0) throw BusinessException.NotFound("Aucun de ces colis ne vous est accessible.");
        var sheet = await ParcelLabels.SheetAsync(db, storage, parcels, request.Format ?? LabelSheet.Format10X10, cancellationToken);
        return ParcelLabels.Pdf(ParcelLabels.Render(pdf, sheet).Content, parcels.Count == 1 ? $"{parcels[0].Code}.pdf" : $"etiquettes-{parcels.Count}.pdf");
    }
}

/// <summary>`LabelJob` sur le fil.</summary>
public sealed record LabelJobDto(string Id, string Status, string Format, int Count, DateTime CreatedAt, DateTime? FinishedAt, DateTime ExpiresAt, int? SizeBytes, string FileType, string? Error)
{
    public static LabelJobDto From(LabelJob row) => new(row.Id, row.Status.ToString().ToLowerInvariant(), row.Format, row.ParcelCount, row.CreatedAt, row.FinishedAt, row.ExpiresAt,
        row.SizeBytes, row.StorageKey?.EndsWith(".zip", StringComparison.Ordinal) == true ? "zip" : "pdf", row.Error);
}

/// <summary>`POST /parcels/label-jobs` : la planche se prépare en arrière-plan (202).</summary>
public sealed record CreateLabelJobCommand : LabelSelection, IRequest<LabelJobDto>;

public sealed class CreateLabelJobValidator : LabelSelectionValidator<CreateLabelJobCommand>;

internal sealed class CreateLabelJobHandler(IAppDbContext db, ICurrentUser caller, IClock clock, LabelJobQueue queue) : IRequestHandler<CreateLabelJobCommand, LabelJobDto>
{
    public async Task<LabelJobDto> Handle(CreateLabelJobCommand request, CancellationToken cancellationToken)
    {
        var codes = await LabelSelections.CodesAsync(db, caller, clock, request, cancellationToken);
        var now = clock.UtcNow;
        var row = new LabelJob
        {
            RequestedById = caller.UserId,
            Format = request.Format ?? LabelSheet.Format10X10,
            ParcelCount = codes.Count,
            CreatedAt = now,
            ExpiresAt = now.AddHours(ParcelLabels.JobTtlHours),
        };
        db.LabelJobs.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        queue.Enqueue(new LabelJobWork(row.Id, codes, row.Format, $"labels/{caller.CompanyId}"));
        return LabelJobDto.From(row);
    }
}

/// <summary>`GET /parcels/label-jobs` : mes travaux encore valables (20 au plus).</summary>
public sealed record GetLabelJobsQuery : IRequest<IReadOnlyList<LabelJobDto>>;

internal sealed class GetLabelJobsHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<GetLabelJobsQuery, IReadOnlyList<LabelJobDto>>
{
    public async Task<IReadOnlyList<LabelJobDto>> Handle(GetLabelJobsQuery request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var rows = await db.LabelJobs.AsNoTracking()
            .Where(j => j.RequestedById == caller.UserId && j.ExpiresAt > now && ParcelLabels.Formats.Contains(j.Format))
            .OrderByDescending(j => j.CreatedAt).Take(20).ToListAsync(cancellationToken);
        return rows.Select(LabelJobDto.From).ToList();
    }
}

/// <summary>`GET /parcels/label-jobs/:id`.</summary>
public sealed record GetLabelJobQuery(string Id) : IRequest<LabelJobDto>;

internal sealed class GetLabelJobHandler(IAppDbContext db, ICurrentUser caller, IClock clock) : IRequestHandler<GetLabelJobQuery, LabelJobDto>
{
    public async Task<LabelJobDto> Handle(GetLabelJobQuery request, CancellationToken cancellationToken) =>
        LabelJobDto.From(await LabelJobFiles.OwnAsync(db, caller, clock, request.Id, cancellationToken));
}

/// <summary>`GET /parcels/label-jobs/:id/file` : le PDF (ou le ZIP) prêt.</summary>
public sealed record GetLabelJobFileQuery(string Id) : IRequest<FileContent>;

internal sealed class GetLabelJobFileHandler(IAppDbContext db, ICurrentUser caller, IClock clock, IFileStorage storage) : IRequestHandler<GetLabelJobFileQuery, FileContent>
{
    public async Task<FileContent> Handle(GetLabelJobFileQuery request, CancellationToken cancellationToken)
    {
        var row = await LabelJobFiles.OwnAsync(db, caller, clock, request.Id, cancellationToken);
        if (row.Status != LabelJobStatus.READY || row.StorageKey is null)
        {
            throw BusinessException.Conflict("Les étiquettes ne sont pas encore prêtes.", new { reason = "label-job-not-ready", status = row.Status.ToString().ToLowerInvariant() });
        }
        var zip = row.StorageKey.EndsWith(".zip", StringComparison.Ordinal);
        var content = await storage.GetAsync(row.StorageKey, cancellationToken);
        return new FileContent(content, zip ? "application/zip" : FileContent.Pdf, $"etiquettes-{row.ParcelCount}.{(zip ? "zip" : "pdf")}") { CacheControl = "private, no-store" };
    }
}

internal static class LabelJobFiles
{
    public static async Task<LabelJob> OwnAsync(IAppDbContext db, ICurrentUser caller, IClock clock, string id, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        return await db.LabelJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id && j.RequestedById == caller.UserId && j.ExpiresAt > now, cancellationToken)
               ?? throw BusinessException.NotFound("Étiquettes introuvables ou expirées.");
    }
}

/// <summary>Le rendu d'un travail de la file (appelé par le service d'arrière-plan).</summary>
public sealed record ProcessLabelJobCommand(LabelJobWork Work) : IRequest;

internal sealed class ProcessLabelJobHandler(IAppDbContext db, IFileStorage storage, IPdfService pdf, IClock clock, ILogger<ProcessLabelJobHandler> logger) : IRequestHandler<ProcessLabelJobCommand>
{
    public async Task Handle(ProcessLabelJobCommand request, CancellationToken cancellationToken)
    {
        var work = request.Work;
        var job = await db.LabelJobs.FirstOrDefaultAsync(j => j.Id == work.JobId, cancellationToken);
        if (job is null) return;
        var startedAt = clock.UtcNow;
        try
        {
            job.Status = LabelJobStatus.RUNNING;
            job.StartedAt = startedAt;
            await db.SaveChangesAsync(cancellationToken);
            var codes = work.Codes;
            var parcels = ParcelLabels.InOrder(await ParcelLabels.LoadAsync(db.Parcels.AsNoTracking().Where(p => codes.Contains(p.Code)), cancellationToken), codes);
            var sheet = await ParcelLabels.SheetAsync(db, storage, parcels, work.Format, cancellationToken);
            var (content, zip) = ParcelLabels.Render(pdf, sheet);
            var stored = await storage.PutAsync(work.Folder, content, zip ? "application/zip" : FileContent.Pdf, cancellationToken);
            var finishedAt = clock.UtcNow;
            job.Status = LabelJobStatus.READY;
            job.StorageKey = stored.Key;
            job.SizeBytes = (int)stored.SizeBytes;
            job.FinishedAt = finishedAt;
            job.ExpiresAt = finishedAt.AddHours(ParcelLabels.JobTtlHours);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Travail {Id} : {Count} lignes en {Ms} ms, {Size} octets.", job.Id, job.ParcelCount, (finishedAt - startedAt).TotalMilliseconds, stored.SizeBytes);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(error, "Étiquettes {Id} : rendu échoué", job.Id);
            job.Status = LabelJobStatus.FAILED;
            job.Error = "failed";
            job.FinishedAt = clock.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }
}

/// <summary>Ménage des travaux : au démarrage, les travaux interrompus ; ensuite, les expirés et leurs fichiers.</summary>
public sealed record SweepLabelJobsCommand(bool Recover) : IRequest<int>;

internal sealed class SweepLabelJobsHandler(IAppDbContext db, IFileStorage storage, IClock clock) : IRequestHandler<SweepLabelJobsCommand, int>
{
    public async Task<int> Handle(SweepLabelJobsCommand request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        if (request.Recover)
        {
            await db.LabelJobs.Where(j => j.Status == LabelJobStatus.PENDING || j.Status == LabelJobStatus.RUNNING)
                .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, LabelJobStatus.FAILED).SetProperty(j => j.Error, "interrupted").SetProperty(j => j.FinishedAt, now), cancellationToken);
        }
        var expired = await db.LabelJobs.Where(j => j.ExpiresAt <= now).ToListAsync(cancellationToken);
        foreach (var row in expired.Where(r => r.StorageKey is not null)) await storage.RemoveAsync(row.StorageKey!, cancellationToken);
        db.LabelJobs.RemoveRange(expired);
        await db.SaveChangesAsync(cancellationToken);
        return expired.Count;
    }
}
