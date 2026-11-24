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

/// <summary>`GET /invoices/:code`.</summary>
public sealed record GetInvoiceQuery(string Code) : IRequest<InvoiceDetailDto>;

internal sealed class GetInvoiceHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetInvoiceQuery, InvoiceDetailDto>
{
    public Task<InvoiceDetailDto> Handle(GetInvoiceQuery request, CancellationToken cancellationToken) => InvoiceRows.DetailAsync(db, caller, request.Code, cancellationToken);
}
