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

/// <summary>`GET /bank-accounts` : les comptes de la société, le compte par défaut marqué.</summary>
public sealed record GetBankAccountsQuery : IRequest<IReadOnlyList<BankAccountDto>>;

internal sealed class GetBankAccountsHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetBankAccountsQuery, IReadOnlyList<BankAccountDto>>
{
    public async Task<IReadOnlyList<BankAccountDto>> Handle(GetBankAccountsQuery request, CancellationToken cancellationToken)
    {
        if (caller.CourierId is not null) return [];
        var setting = await db.SettingAsync(SettingKeys.DefaultBankAccount, cancellationToken);
        var defaultId = setting is { ValueKind: System.Text.Json.JsonValueKind.String } value ? value.GetString() : string.Empty;
        var rows = await db.BankAccounts.AsNoTracking().Where(a => a.DeletedAt == null).OrderBy(a => a.Label)
            .Select(a => new { a.Id, a.BankId, Bank = a.Bank.Name, a.Label, a.Rib, a.Holder, a.IsActive }).ToListAsync(cancellationToken);
        return rows.Select(a => new BankAccountDto(a.Id, a.BankId, a.Bank, a.Label, a.Rib, a.Holder, a.IsActive, a.Id == defaultId)).ToList();
    }
}
