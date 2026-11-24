using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.CourierApp;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Application.Routes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using Delivery.Domain.Pricing;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Returns;

/// <summary>`GET /return-slips/:code/candidates`.</summary>
public sealed record GetSlipCandidatesQuery(string Code, string? Search, string? Page, string? PageSize) : IRequest<object>;

public sealed class GetSlipCandidatesValidator : ZodValidator<GetSlipCandidatesQuery>
{
    public GetSlipCandidatesValidator()
    {
        RuleFor(x => x.Search).MaxLength(120);
    }
}

internal sealed class GetSlipCandidatesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetSlipCandidatesQuery, object>
{
    public async Task<object> Handle(GetSlipCandidatesQuery request, CancellationToken cancellationToken)
    {
        var rows = await Slips.CandidatesAsync(db, caller, request.Code, request.Search, cancellationToken);
        return string.IsNullOrEmpty(request.Page) ? rows : RouteWrites.Page(rows, request.Page, request.PageSize);
    }
}
