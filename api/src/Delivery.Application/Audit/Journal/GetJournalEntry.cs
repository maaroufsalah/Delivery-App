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

/// <summary>`GET /audit/journal/entry/:id` : une entrée et son détail.</summary>
public sealed record GetJournalEntryQuery(string Id) : IRequest<JournalEntryDetailDto>;

internal sealed class GetJournalEntryHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetJournalEntryQuery, JournalEntryDetailDto>
{
    public async Task<JournalEntryDetailDto> Handle(GetJournalEntryQuery request, CancellationToken cancellationToken)
    {
        if (caller.CourierId is not null) throw BusinessException.NotFound("Entrée de journal introuvable.");
        var entries = await JournalReader.PresentAsync(db, caller, [request.Id], cancellationToken);
        return entries.FirstOrDefault() ?? throw BusinessException.NotFound("Entrée de journal introuvable.");
    }
}
