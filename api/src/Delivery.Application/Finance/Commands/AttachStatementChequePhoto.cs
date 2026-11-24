using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.PettyCash;
using Delivery.Application.Returns;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Finance;

/// <summary>`chequePhotoSchema` : la photo du chèque d'un relevé réglé ; l'ancienne est effacée.</summary>
public sealed record AttachStatementChequePhotoCommand : IRequest<CourierStatementDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public ProofFileDto? Proof { get; init; }
}

public sealed class AttachStatementChequePhotoValidator : ZodValidator<AttachStatementChequePhotoCommand>
{
    public AttachStatementChequePhotoValidator()
    {
        RuleFor(x => x.Proof).Required("object");
        ProofRules.Proof(this, x => x.Proof);
    }
}

internal sealed class AttachStatementChequePhotoHandler(IAppDbContext db, ICurrentUser caller, IFileStorage storage) : IRequestHandler<AttachStatementChequePhotoCommand, CourierStatementDetailDto>
{
    public async Task<CourierStatementDetailDto> Handle(AttachStatementChequePhotoCommand input, CancellationToken cancellationToken)
    {
        var statement = await CourierStatements.Scoped(db.CourierStatements.AsNoTracking(), caller).Where(s => s.Code == input.Code).Select(s => new { s.Id, s.Status, s.ProofPath })
                            .FirstOrDefaultAsync(cancellationToken) ?? throw BusinessException.NotFound($"Relevé introuvable : {input.Code}");
        if (statement.Status != CourierStatementStatus.PAID)
        {
            throw BusinessException.Conflict("Ce relevé n’est pas encore réglé : il n’y a pas de chèque à joindre.", new { reason = "statement-not-paid" });
        }
        var stored = await storage.PutAsync($"cheques/{caller.CompanyId}", Convert.FromBase64String(input.Proof!.Content!), input.Proof.MimeType!, cancellationToken);
        await db.CourierStatements.Where(s => s.Id == statement.Id).ExecuteUpdateAsync(set => set.SetProperty(s => s.ProofName, input.Proof.Name)
            .SetProperty(s => s.ProofMimeType, input.Proof.MimeType).SetProperty(s => s.ProofPath, stored.Key), cancellationToken);
        if (statement.ProofPath is not null) await storage.RemoveAsync(statement.ProofPath, cancellationToken);
        return await CourierStatements.DetailAsync(db, caller, input.Code, cancellationToken);
    }
}
