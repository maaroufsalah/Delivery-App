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

/// <summary>`GET /routes/:code/note-candidates`.</summary>
public sealed record GetRouteNoteCandidatesQuery(string Code, string? Search, string? Page, string? PageSize) : IRequest<object>;

internal sealed class GetRouteNoteCandidatesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetRouteNoteCandidatesQuery, object>
{
    public async Task<object> Handle(GetRouteNoteCandidatesQuery request, CancellationToken cancellationToken)
    {
        var rows = await RouteWrites.NoteCandidatesAsync(db, caller, request.Code, request.Search, cancellationToken);
        return string.IsNullOrEmpty(request.Page) ? rows : RouteWrites.Page(rows, request.Page, request.PageSize);
    }
}
