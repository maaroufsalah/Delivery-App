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

/// <summary>`GET /courier/me/balance` : son solde, sans identifiant (la portée le donne).</summary>
public sealed record GetMyBalanceQuery : IRequest<CourierBalanceDto>;

internal sealed class GetMyBalanceHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetMyBalanceQuery, CourierBalanceDto>
{
    public Task<CourierBalanceDto> Handle(GetMyBalanceQuery request, CancellationToken cancellationToken) => Remittances.BalanceAsync(db, caller.CourierId ?? string.Empty, cancellationToken);
}
