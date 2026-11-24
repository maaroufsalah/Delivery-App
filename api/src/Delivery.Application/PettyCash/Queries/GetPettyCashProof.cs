using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Idempotency;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.PettyCash;

/// <summary>`GET /petty-cash/:id/proof` : le justificatif tel qu'il a été déposé.</summary>
public sealed record GetPettyCashProofQuery(string Id) : IRequest<FileContent>;

internal sealed class GetPettyCashProofHandler(IAppDbContext db, ICurrentUser caller, IFileStorage storage) : IRequestHandler<GetPettyCashProofQuery, FileContent>
{
    public async Task<FileContent> Handle(GetPettyCashProofQuery request, CancellationToken cancellationToken)
    {
        var row = await PettyCashMovements.Scoped(db.PettyCashMovements.AsNoTracking(), caller).Where(m => m.Id == request.Id).Select(m => new { m.ProofPath, m.ProofName, m.ProofMimeType })
                      .FirstOrDefaultAsync(cancellationToken) ?? throw BusinessException.NotFound("Mouvement de petite caisse introuvable.");
        if (row.ProofPath is null) throw BusinessException.NotFound("Aucun justificatif sur ce mouvement.", new { reason = "no-proof" });
        var content = await storage.GetAsync(row.ProofPath, cancellationToken);
        return new FileContent(content, row.ProofMimeType ?? "application/octet-stream", Uri.EscapeDataString(row.ProofName ?? "justificatif"), CacheControl: "private, no-store");
    }
}
