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
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.Returns;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Finance;

/// <summary>`GET /remittances/:code`.</summary>
public sealed record GetRemittanceQuery(string Code) : IRequest<RemittanceDetailDto>;

internal sealed class GetRemittanceHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetRemittanceQuery, RemittanceDetailDto>
{
    public Task<RemittanceDetailDto> Handle(GetRemittanceQuery request, CancellationToken cancellationToken) => Remittances.DetailAsync(db, caller, request.Code, cancellationToken);
}
