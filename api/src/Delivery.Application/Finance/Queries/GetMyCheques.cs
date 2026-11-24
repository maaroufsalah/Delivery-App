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

namespace Delivery.Application.Finance;

/// <summary>`GET /courier/me/cheques` : ses relevés réglés par chèque, les 100 derniers.</summary>
public sealed record GetMyChequesQuery : IRequest<IReadOnlyList<CourierChequeDto>>;

internal sealed class GetMyChequesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetMyChequesQuery, IReadOnlyList<CourierChequeDto>>
{
    public async Task<IReadOnlyList<CourierChequeDto>> Handle(GetMyChequesQuery request, CancellationToken cancellationToken)
    {
        var courierId = caller.CourierId ?? throw BusinessException.Forbidden("Cet espace est réservé aux livreurs.", new { reason = "not-a-courier" });
        var rows = await db.CourierStatements.AsNoTracking().Where(s => s.CourierId == courierId && s.PaymentMethod == PaymentMethod.CHECK && s.ChequeNumber != null)
            .OrderByDescending(s => s.CreatedAt).Take(100).Select(s => new { s.Id, s.Code, s.ChequeNumber, s.ChequeBank, s.ChequeDate, s.TotalDue, s.PaidAt, s.ProofPath })
            .ToListAsync(cancellationToken);
        return rows.Select(s => new CourierChequeDto(s.Id, s.Code, s.ChequeNumber ?? string.Empty, s.ChequeBank,
            s.ChequeDate is { } d ? DateTime.SpecifyKind(d.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc) : null, Money.ToCents(s.TotalDue), s.PaidAt is not null, s.ProofPath is not null)).ToList();
    }
}
