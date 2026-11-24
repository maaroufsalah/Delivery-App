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

/// <summary>`GET /courier/me/remittance` : sa caisse ouverte, ou `null`.</summary>
public sealed record GetMyOpenRemittanceQuery : IRequest<CourierOpenRemittanceDto>;

internal sealed class GetMyOpenRemittanceHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetMyOpenRemittanceQuery, CourierOpenRemittanceDto>
{
    public async Task<CourierOpenRemittanceDto> Handle(GetMyOpenRemittanceQuery request, CancellationToken cancellationToken)
    {
        var code = await Remittances.Scoped(db.CashRemittances.AsNoTracking(), caller).Where(r => r.Status == CashRemittanceStatus.DRAFT && r.CourierClosedAt == null).OrderBy(r => r.CreatedAt)
            .Select(r => r.Code).FirstOrDefaultAsync(cancellationToken);
        return new CourierOpenRemittanceDto(code is null ? null : await Remittances.DetailAsync(db, caller, code, cancellationToken));
    }
}
