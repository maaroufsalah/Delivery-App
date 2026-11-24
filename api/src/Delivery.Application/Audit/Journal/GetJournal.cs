using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Audit.Journal;

/// <summary>`GET /audit/journal` et `GET /audit/journal/entity/:entity/:entityId`.</summary>
public sealed record GetJournalQuery(JournalFilter Filter) : IRequest<Paginated<JournalEntryDto>>;

internal sealed class GetJournalHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetJournalQuery, Paginated<JournalEntryDto>>
{
    public async Task<Paginated<JournalEntryDto>> Handle(GetJournalQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await JournalReader.PageAsync(db, caller, request.Filter, cancellationToken);
        return new Paginated<JournalEntryDto>(items.Select(i => i.Summary()).ToList(), total, request.Filter.Page, request.Filter.PageSize);
    }
}
