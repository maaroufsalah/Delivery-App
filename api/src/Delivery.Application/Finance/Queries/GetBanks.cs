using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Application.Parcels;
using Delivery.Domain.Common;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Finance;

/// <summary>`GET /banks` : le référentiel bancaire (rien pour un livreur).</summary>
public sealed record GetBanksQuery : IRequest<IReadOnlyList<BankDto>>;

internal sealed class GetBanksHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetBanksQuery, IReadOnlyList<BankDto>>
{
    public async Task<IReadOnlyList<BankDto>> Handle(GetBanksQuery request, CancellationToken cancellationToken)
    {
        if (caller.CourierId is not null) return [];
        return await db.Banks.AsNoTracking().Where(b => b.DeletedAt == null).OrderBy(b => b.Name).Select(b => new BankDto(b.Id, b.Name, b.Code, b.IsActive)).ToListAsync(cancellationToken);
    }
}
