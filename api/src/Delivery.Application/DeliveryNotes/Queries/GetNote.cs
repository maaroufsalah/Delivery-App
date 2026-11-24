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

/// <summary>`GET /delivery-notes/:code`.</summary>
public sealed record GetNoteQuery(string Code) : IRequest<DeliveryNoteDetailDto>;

internal sealed class GetNoteHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetNoteQuery, DeliveryNoteDetailDto>
{
    public Task<DeliveryNoteDetailDto> Handle(GetNoteQuery request, CancellationToken cancellationToken) => NoteRows.DetailAsync(db, caller, request.Code, cancellationToken);
}
