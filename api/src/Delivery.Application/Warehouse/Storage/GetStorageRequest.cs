using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.Warehouse.Stock;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Warehouse.Storage;

/// <summary>`GET /storage-requests/:code`.</summary>
public sealed record GetStorageRequestQuery(string Code) : IRequest<StorageRequestDetailDto>;

internal sealed class GetStorageRequestHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetStorageRequestQuery, StorageRequestDetailDto>
{
    public Task<StorageRequestDetailDto> Handle(GetStorageRequestQuery request, CancellationToken cancellationToken) => StorageRows.DetailAsync(db, caller, request.Code, cancellationToken);
}
