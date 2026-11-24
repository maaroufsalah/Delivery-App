using System.Text.Json;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Persistence;
using Delivery.Application.Common.Validation;
using Delivery.Application.Parcels.Commands;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Parcels.Import;

/// <summary>`POST /parcels/import` : tout ou rien, ou les seules lignes valides ; journalisé.</summary>
public sealed record RunParcelImportCommand : ImportSource, IRequest<ParcelImportResultDto>
{
    public string? Mode { get; init; } = "all-or-nothing";
}

public sealed class RunParcelImportValidator : ImportSourceValidator<RunParcelImportCommand>
{
    public RunParcelImportValidator()
    {
        RuleFor(x => x.Mode).OneOf(["all-or-nothing", "valid-only"]);
    }
}

internal sealed class RunParcelImportHandler(IAppDbContext db, ICurrentUser caller, IExcelService excel, IFileStorage storage, IClock clock, ILogger<RunParcelImportHandler> logger)
    : IRequestHandler<RunParcelImportCommand, ParcelImportResultDto>
{
    public async Task<ParcelImportResultDto> Handle(RunParcelImportCommand input, CancellationToken cancellationToken)
    {
        var context = await ParcelImports.ContextAsync(db, caller, input.ClientId, cancellationToken);
        var (items, ready) = await ParcelImports.ValidateAsync(db, context, ParcelImports.ReadRows(excel, input.Content, input.Rows), cancellationToken);
        var errors = items.SelectMany(i => i.Errors).ToList();
        var warnings = items.SelectMany(i => i.Warnings ?? []).ToList();
        var validOnly = input.Mode == "valid-only";
        if (!validOnly && errors.Count > 0)
        {
            throw BusinessException.Validation($"Import refusé : {errors.Count} ligne(s) en erreur. Corrigez-les, ou choisissez « importer les lignes valides ».",
                new { errors = errors.Take(200), rows = items.Count, invalid = items.Count - ready.Count });
        }
        if (ready.Count == 0) throw BusinessException.Validation("Aucune ligne valide à importer.", new { errors = errors.Take(200) });
        string? storageKey = null;
        if (!string.IsNullOrEmpty(input.Content))
        {
            try
            {
                storageKey = (await storage.PutAsync($"{caller.CompanyId}/imports", Convert.FromBase64String(input.Content), FileContent.Xlsx, cancellationToken)).Key;
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                logger.LogWarning(error, "archivage du classeur d’import impossible");
            }
        }
        var author = await ParcelStatusChange.AuthorNameAsync(db, caller.UserId, cancellationToken);
        var filename = input.Filename ?? "import.xlsx";
        var journal = new ParcelImport
        {
            ClientId = context.ClientId,
            Filename = filename.Length > 200 ? filename[..200] : filename,
            StorageKey = storageKey,
            Mode = validOnly ? ParcelImportMode.VALID_ONLY : ParcelImportMode.ALL_OR_NOTHING,
            RowCount = items.Count,
            OkCount = 0,
            KoCount = items.Count - ready.Count,
            Errors = JsonSerializer.Serialize(errors.Concat(warnings).Take(2000), JsonSerializerOptions.Web),
            CreatedById = caller.UserId,
            AuthorName = author,
        };
        db.ParcelImports.Add(journal);
        await db.SaveChangesAsync(cancellationToken);
        var imported = ready.Select(r => r.Row).ToHashSet();
        try
        {
            var codes = await ParcelImports.WriteAsync(db, caller, context, journal.Id, ready, author, clock.UtcNow, cancellationToken);
            journal.OkCount = codes.Count;
            await db.SaveChangesAsync(cancellationToken);
            return new ParcelImportResultDto(journal.Id, codes.Count, items.Count - ready.Count, warnings.Select(w => w.Row).Where(imported.Contains).Distinct().Count(), codes);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            db.ChangeTracker.Clear();
            await db.ParcelImports.Where(i => i.Id == journal.Id).ExecuteUpdateAsync(s => s.SetProperty(i => i.OkCount, 0).SetProperty(i => i.KoCount, items.Count), CancellationToken.None);
            if (DbErrors.IsUniqueViolation(error))
            {
                throw BusinessException.Conflict("Référence déjà utilisée : une référence de ce fichier a été prise entre l’aperçu et l’import. Relancez l’aperçu.",
                    new { reason = "duplicate-external-code", field = "externalCode" });
            }
            throw;
        }
    }
}
