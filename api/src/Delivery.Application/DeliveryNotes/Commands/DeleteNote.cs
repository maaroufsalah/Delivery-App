using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.Parcels.Commands;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.DeliveryNotes;

/// <summary>`DELETE /delivery-notes/:code` : ni validé, ni chez le livreur, ni sur un circuit.</summary>
public sealed record DeleteNoteCommand(string Code) : IRequest<NoteCodeDto>;

internal sealed class DeleteNoteHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<DeleteNoteCommand, NoteCodeDto>
{
    public Task<NoteCodeDto> Handle(DeleteNoteCommand request, CancellationToken cancellationToken) => NoteDeletion.DeleteAsync(db, caller, request.Code, cancellationToken);
}
