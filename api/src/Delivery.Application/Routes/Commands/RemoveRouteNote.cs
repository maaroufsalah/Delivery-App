using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Routes;

/// <summary>`DELETE /routes/:code/notes/:noteId`.</summary>
public sealed record RemoveRouteNoteCommand(string Code, string NoteId) : IRequest<RouteDetailDto>;

internal sealed class RemoveRouteNoteHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<RemoveRouteNoteCommand, RouteDetailDto>
{
    public async Task<RouteDetailDto> Handle(RemoveRouteNoteCommand request, CancellationToken cancellationToken)
    {
        var route = await RouteRows.FindAsync(db, caller, request.Code, cancellationToken);
        RouteWrites.AssertDraft(route.Status);
        await RouteWrites.RemoveNoteAsync(db, route, request.NoteId, cancellationToken);
        return await RouteRows.DetailAsync(db, caller, request.Code, cancellationToken);
    }
}
