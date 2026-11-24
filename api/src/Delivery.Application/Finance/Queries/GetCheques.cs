using System.Globalization;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Validation;
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Returns;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Finance;

/// <summary>`GET /cheques`.</summary>
public sealed record GetChequesQuery(ChequeListFilter Filter) : IRequest<ChequePageDto>;

public sealed class GetChequesValidator : ZodValidator<GetChequesQuery>
{
    public GetChequesValidator()
    {
        RuleFor(x => x.Filter.Bank).MaxLength(120).OverridePropertyName("bank");
    }
}

internal sealed class GetChequesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetChequesQuery, ChequePageDto>
{
    public async Task<ChequePageDto> Handle(GetChequesQuery request, CancellationToken cancellationToken)
    {
        var where = ChequeLists.Filter(db, caller, request.Filter);
        var paging = request.Filter.Paging;
        var total = await where.CountAsync(cancellationToken);
        var sum = await where.SumAsync(c => (decimal?)c.Amount, cancellationToken);
        var items = await Cheques.ListAsync(where.OrderByDescending(c => c.ReceivedAt).Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize), caller, cancellationToken);
        return new ChequePageDto(items, total, paging.Page, paging.PageSize, sum is null ? 0 : Money.ToCents(sum.Value));
    }
}
