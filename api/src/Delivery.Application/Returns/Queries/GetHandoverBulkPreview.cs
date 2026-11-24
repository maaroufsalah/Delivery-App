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

/// <summary>`GET /return-handovers/bulk` : ce que la création en masse ouvrirait.</summary>
public sealed record GetHandoverBulkPreviewQuery : IRequest<ReturnBulkSummaryDto>;

internal sealed class GetHandoverBulkPreviewHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetHandoverBulkPreviewQuery, ReturnBulkSummaryDto>
{
    public async Task<ReturnBulkSummaryDto> Handle(GetHandoverBulkPreviewQuery request, CancellationToken cancellationToken)
    {
        ReturnRows.AssertBackOffice(caller);
        return ReturnRows.Summarize(await Handovers.BulkGroupsAsync(db, caller, cancellationToken), []);
    }
}
