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

/// <summary>`GET /delivery-notes`.</summary>
public sealed record GetNotesQuery(NoteListFilter Filter) : IRequest<Paginated<DeliveryNoteListItemDto>>;

internal sealed class GetNotesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetNotesQuery, Paginated<DeliveryNoteListItemDto>>
{
    public async Task<Paginated<DeliveryNoteListItemDto>> Handle(GetNotesQuery request, CancellationToken cancellationToken)
    {
        var paging = request.Filter.Paging;
        var policy = await NoteRules.PolicyAsync(db, caller.AgencyId, cancellationToken);
        var where = NoteRows.Filter(db.DeliveryNotes.AsNoTracking(), caller, request.Filter);
        var total = await where.CountAsync(cancellationToken);
        var rows = await NoteRows.ProjectAsync(where.OrderByDescending(n => n.CreatedAt).Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize), cancellationToken);
        return new Paginated<DeliveryNoteListItemDto>(rows.Select(r => NoteRows.Fill(new DeliveryNoteListItemDto(), r, policy)).ToList(), total, paging.Page, paging.PageSize);
    }
}
