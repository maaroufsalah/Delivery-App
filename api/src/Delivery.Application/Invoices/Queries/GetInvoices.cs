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

/// <summary>`GET /invoices` et `GET /invoices-pro`.</summary>
public sealed record GetInvoicesQuery(InvoiceListFilter Filter) : IRequest<Paginated<InvoiceListItemDto>>;

internal sealed class GetInvoicesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetInvoicesQuery, Paginated<InvoiceListItemDto>>
{
    public async Task<Paginated<InvoiceListItemDto>> Handle(GetInvoicesQuery request, CancellationToken cancellationToken)
    {
        var where = InvoiceLists.Where(db, caller, request.Filter);
        var paging = request.Filter.Paging;
        var total = await where.CountAsync(cancellationToken);
        var items = await InvoiceRows.Project(InvoiceLists.Ordered(where, paging).Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize)).ToListAsync(cancellationToken);
        return new Paginated<InvoiceListItemDto>(items, total, paging.Page, paging.PageSize);
    }
}
