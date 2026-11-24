using System.Globalization;
using System.Text.Json;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Invoices;

/// <summary>`GET /transfer-orders/:code`.</summary>
public sealed record GetTransferOrderQuery(string Code) : IRequest<TransferOrderDetailDto>;

internal sealed class GetTransferOrderHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetTransferOrderQuery, TransferOrderDetailDto>
{
    public Task<TransferOrderDetailDto> Handle(GetTransferOrderQuery request, CancellationToken cancellationToken) => TransferOrders.DetailAsync(db, caller, request.Code, cancellationToken);
}
