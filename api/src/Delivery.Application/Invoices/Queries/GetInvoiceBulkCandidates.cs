using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Finance;
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.PettyCash;
using Delivery.Application.Returns;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Invoices;

/// <summary>`GET /invoices/bulk-candidates` (et Pro) : par vendeur, ce qu'une facture en masse porterait.</summary>
public sealed record GetInvoiceBulkCandidatesQuery(string Kind) : IRequest<IReadOnlyList<BulkInvoiceCandidateDto>>;

internal sealed class GetInvoiceBulkCandidatesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetInvoiceBulkCandidatesQuery, IReadOnlyList<BulkInvoiceCandidateDto>>
{
    public async Task<IReadOnlyList<BulkInvoiceCandidateDto>> Handle(GetInvoiceBulkCandidatesQuery request, CancellationToken cancellationToken) =>
        (await InvoiceRows.PendingByClientAsync(db, caller, null, request.Kind, false, cancellationToken))
        .Select(r => new BulkInvoiceCandidateDto(r.ClientId, r.ClientName, r.AgencyId, r.AgencyName, r.ParcelCount, r.Due)).ToList();
}
