using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoMapper;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Application.Parcels.Dtos;
using Delivery.Application.Parcels.Queries;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Parcels.Commands;

/// <summary>`POST /parcels/:code/notes` : note interne.</summary>
public sealed record AddParcelNoteCommand : IRequest<ParcelNoteDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public string? Body { get; init => field = Zod.Trim(value); }
}

public sealed class AddParcelNoteValidator : ZodValidator<AddParcelNoteCommand>
{
    public AddParcelNoteValidator()
    {
        RuleFor(x => x.Body).Required().MinLength(1, "required").MaxLength(1000);
    }
}

internal sealed class AddParcelNoteHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<AddParcelNoteCommand, ParcelNoteDto>
{
    public async Task<ParcelNoteDto> Handle(AddParcelNoteCommand input, CancellationToken cancellationToken)
    {
        var parcelId = await ParcelFilters.Scoped(db.Parcels.AsNoTracking(), caller).Where(p => p.Code == input.Code).Select(p => p.Id).FirstOrDefaultAsync(cancellationToken)
                       ?? throw BusinessException.NotFound($"Colis introuvable : {input.Code}");
        var note = new ParcelNote
        {
            ParcelId = parcelId,
            Body = input.Body!,
            AuthorId = caller.UserId,
            AuthorName = await ParcelStatusChange.AuthorNameAsync(db, caller.UserId, cancellationToken),
        };
        db.ParcelNotes.Add(note);
        await db.SaveChangesAsync(cancellationToken);
        return new ParcelNoteDto(note.Id, note.ParcelId, note.Body, note.AuthorName, note.AttachmentName, note.CreatedAt);
    }
}
