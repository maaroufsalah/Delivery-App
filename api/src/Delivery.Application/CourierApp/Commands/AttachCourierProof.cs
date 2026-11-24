using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Idempotency;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Application.Routes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using Delivery.Domain.Parcels;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.CourierApp;

public sealed record AttachCourierProofCommand : IRequest<object?>
{
    public static readonly string[] Types = ["image/jpeg", "image/png", "image/webp"];

    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    [JsonIgnore]
    public string? IdempotencyKey { get; init; }

    public ProofPhotoDto? Proof { get; init; }
}

public sealed class AttachCourierProofValidator : ZodValidator<AttachCourierProofCommand>
{
    public AttachCourierProofValidator()
    {
        RuleFor(x => x.Proof).Required("object");
        RuleFor(x => x.Proof!.Name).Required().MinLength(1, "required").MaxLength(120).OverridePropertyName("proof.name").When(x => x.Proof is not null);
        RuleFor(x => x.Proof!.MimeType).Must(v => v is not null && AttachCourierProofCommand.Types.Contains(v)).WithMessage("unsupported-file-type").OverridePropertyName("proof.mimeType")
            .When(x => x.Proof is not null);
        RuleFor(x => x.Proof!.Content).Required().MinLength(1, "required").OverridePropertyName("proof.content").When(x => x.Proof is not null);
    }
}

internal sealed class AttachCourierProofHandler(IAppDbContext db, ICurrentUser caller, IFileStorage storage) : IRequestHandler<AttachCourierProofCommand, object?>
{
    public Task<object?> Handle(AttachCourierProofCommand request, CancellationToken cancellationToken) =>
        IdempotentReplay.RunAsync(db, caller, request.IdempotencyKey, $"POST /courier/parcels/{request.Code}/proof", () => AttachAsync(request, cancellationToken), cancellationToken);

    private async Task<ProofAttachedDto> AttachAsync(AttachCourierProofCommand request, CancellationToken cancellationToken)
    {
        var located = await CourierParcels.LocateAsync(db, caller, request.Code, cancellationToken);
        var evt = await db.ParcelEvents.Where(e => e.ParcelId == located.ParcelId && e.AuthorId == caller.UserId).OrderByDescending(e => e.CreatedAt).FirstOrDefaultAsync(cancellationToken)
                  ?? throw BusinessException.Conflict("Rapportez d’abord une issue : une photo seule ne prouve rien.", new { reason = "no-event-to-attach" });
        byte[] content;
        try
        {
            content = Convert.FromBase64String(request.Proof!.Content!);
        }
        catch (FormatException)
        {
            content = [];
        }
        var stored = await storage.PutAsync($"proofs/{caller.CompanyId}", content, request.Proof!.MimeType!, cancellationToken);
        var previous = evt.AttachmentPath;
        evt.AttachmentName = request.Proof!.Name;
        evt.AttachmentMimeType = request.Proof.MimeType;
        evt.AttachmentPath = stored.Key;
        await db.SaveChangesAsync(cancellationToken);
        if (previous is not null) await storage.RemoveAsync(previous, cancellationToken);
        return new ProofAttachedDto(true);
    }
}
