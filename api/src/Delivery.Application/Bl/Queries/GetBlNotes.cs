using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Bl;

/// <summary>`GET /bl/notes`.</summary>
public sealed record GetBlNotesQuery(BlNoteFilter Filter) : IRequest<Paginated<BlNoteListItemDto>>;

internal sealed class GetBlNotesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetBlNotesQuery, Paginated<BlNoteListItemDto>>
{
    public async Task<Paginated<BlNoteListItemDto>> Handle(GetBlNotesQuery request, CancellationToken cancellationToken)
    {
        BlNoteLists.Validate(request.Filter);
        var paging = request.Filter.Paging;
        var where = BlNoteLists.Filter(db, caller, request.Filter);
        var total = await where.CountAsync(cancellationToken);
        var items = await BlRows.ProjectNotes(where.Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize)).ToListAsync(cancellationToken);
        return new Paginated<BlNoteListItemDto>(items, total, paging.Page, paging.PageSize);
    }
}
