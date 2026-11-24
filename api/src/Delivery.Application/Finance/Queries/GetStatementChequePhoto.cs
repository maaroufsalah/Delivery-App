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

/// <summary>`GET /courier-statements/:code/cheque-photo` : l'image se regarde (`inline`).</summary>
public sealed record GetStatementChequePhotoQuery(string Code) : IRequest<FileContent>;

internal sealed class GetStatementChequePhotoHandler(IAppDbContext db, ICurrentUser caller, IFileStorage storage) : IRequestHandler<GetStatementChequePhotoQuery, FileContent>
{
    public async Task<FileContent> Handle(GetStatementChequePhotoQuery request, CancellationToken cancellationToken)
    {
        var statement = await CourierStatements.Scoped(db.CourierStatements.AsNoTracking(), caller).Where(s => s.Code == request.Code).Select(s => new { s.ProofName, s.ProofMimeType, s.ProofPath })
                            .FirstOrDefaultAsync(cancellationToken) ?? throw BusinessException.NotFound($"Relevé introuvable : {request.Code}");
        if (statement.ProofPath is null) throw BusinessException.NotFound("Aucune photo de chèque sur ce relevé.", new { reason = "no-cheque-photo" });
        return new FileContent(await storage.GetAsync(statement.ProofPath, cancellationToken), statement.ProofMimeType ?? "application/octet-stream", statement.ProofName ?? "cheque", Inline: true,
            CacheControl: "private, no-store");
    }
}
