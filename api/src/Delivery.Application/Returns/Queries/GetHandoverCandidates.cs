using System.Globalization;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.Routes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Returns;

/// <summary>`GET /return-handovers/candidates` : la liste complète, ou une page avec son vrai total.</summary>
public sealed record GetHandoverCandidatesQuery(string? CourierId, string? Search, string? Page, string? PageSize) : IRequest<object>;

public sealed class GetHandoverCandidatesValidator : ZodValidator<GetHandoverCandidatesQuery>
{
    public GetHandoverCandidatesValidator()
    {
        RuleFor(x => x.Search).MaxLength(120);
        RuleFor(x => x.CourierId).MinLength(1);
    }
}

internal sealed class GetHandoverCandidatesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetHandoverCandidatesQuery, object>
{
    public async Task<object> Handle(GetHandoverCandidatesQuery request, CancellationToken cancellationToken)
    {
        var rows = await Handovers.CandidatesAsync(db, caller, request.CourierId, request.Search, cancellationToken);
        return string.IsNullOrEmpty(request.Page) ? rows : RouteWrites.Page(rows, request.Page, request.PageSize);
    }
}
