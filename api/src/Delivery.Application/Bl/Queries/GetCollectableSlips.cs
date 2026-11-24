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

/// <summary>`GET /bl/slips` (`blSlipListQuerySchema`) : les BL encore en sacoche, libres de tout bon.</summary>
public sealed record GetCollectableSlipsQuery(string? ClientId, string? CourierId) : IRequest<IReadOnlyList<DeliverySlipListItemDto>>;

internal sealed class GetCollectableSlipsHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetCollectableSlipsQuery, IReadOnlyList<DeliverySlipListItemDto>>
{
    public async Task<IReadOnlyList<DeliverySlipListItemDto>> Handle(GetCollectableSlipsQuery request, CancellationToken cancellationToken)
    {
        var slips = db.DeliverySlips.AsNoTracking().Where(s => s.Status == DeliverySlipStatus.COLLECTED && !db.BlNoteItems.Any(i => i.ReservedSlipId == s.Id));
        if (caller.ClientId is { } clientId) slips = slips.Where(s => s.ClientId == clientId);
        if (caller.CourierId is { } courierId) slips = slips.Where(s => s.CourierId == courierId);
        if (!string.IsNullOrEmpty(request.ClientId) && caller.MayFilterBy(caller.ClientId)) slips = slips.Where(s => s.ClientId == request.ClientId);
        if (!string.IsNullOrEmpty(request.CourierId) && caller.MayFilterBy(caller.CourierId)) slips = slips.Where(s => s.CourierId == request.CourierId);
        return await BlRows.Project(slips.OrderBy(s => s.CollectedAt).Take(NoteRules.SelectionMax + 1)).ToListAsync(cancellationToken);
    }
}
