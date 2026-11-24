using AutoMapper;
using Delivery.Application.Clients.Dtos;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Clients.Commands;

/// <summary>`POST /clients/:id/documents` : pièce jointe (PDF ou image, 8 Mo au plus).</summary>
public sealed record AddClientDocumentCommand : IRequest<ClientDetailDto>
{
    public const int MaxBytes = 8 * 1024 * 1024;

    public static readonly string[] Kinds = ["identity", "tax", "contract", "rib", "other"];
    public static readonly string[] Types = ["application/pdf", "image/jpeg", "image/png", "image/webp"];

    [System.Text.Json.Serialization.JsonIgnore]
    public string ClientId { get; init; } = string.Empty;

    public string? Name { get; init => field = Zod.Trim(value); }

    public string? Kind { get; init; } = "other";

    public string? MimeType { get; init; }

    public string? Content { get; init; }
}

public sealed class AddClientDocumentValidator : ZodValidator<AddClientDocumentCommand>
{
    public AddClientDocumentValidator()
    {
        RuleFor(x => x.Name).Required().MinLength(1, "required").MaxLength(120);
        RuleFor(x => x.Kind).OneOf(AddClientDocumentCommand.Kinds);
        RuleFor(x => x.MimeType).Must(t => t is not null && AddClientDocumentCommand.Types.Contains(t)).WithMessage("unsupported-file-type");
        RuleFor(x => x.Content).Required().MinLength(1, "required");
    }
}

internal sealed class AddClientDocumentHandler(IAppDbContext db, ICurrentUser caller, IFileStorage storage, IMapper mapper) : IRequestHandler<AddClientDocumentCommand, ClientDetailDto>
{
    public async Task<ClientDetailDto> Handle(AddClientDocumentCommand input, CancellationToken cancellationToken)
    {
        await ClientRows.FindAsync(db, input.ClientId, cancellationToken);
        byte[] content;
        try
        {
            content = Convert.FromBase64String(input.Content!);
        }
        catch (FormatException)
        {
            content = [];
        }
        if (content.Length == 0) throw BusinessException.Validation("Fichier vide.", new { field = "content" });
        if (content.Length > AddClientDocumentCommand.MaxBytes) throw BusinessException.Validation("Fichier trop volumineux (8 Mo maximum).", new { field = "content" });
        var company = await db.Companies.Select(c => c.Id).FirstAsync(cancellationToken);
        var stored = await storage.PutAsync($"{company}/clients/{input.ClientId}", content, input.MimeType!, cancellationToken);
        try
        {
            db.ClientDocuments.Add(new ClientDocument
            {
                ClientId = input.ClientId,
                Name = input.Name!,
                Kind = Enum.Parse<ClientDocumentKind>(input.Kind!.ToUpperInvariant()),
                StorageKey = stored.Key,
                MimeType = stored.MimeType,
                SizeBytes = (int)stored.SizeBytes,
                UploadedById = caller.UserId,
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await storage.RemoveAsync(stored.Key, cancellationToken);
            throw;
        }
        return await ClientRows.DetailAsync(db, caller, mapper, input.ClientId, cancellationToken);
    }
}
