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

/// <summary>`GET /cheques/:id`.</summary>
public sealed record GetChequeQuery(string Id) : IRequest<ChequeDetailDto>;

internal sealed class GetChequeHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetChequeQuery, ChequeDetailDto>
{
    public Task<ChequeDetailDto> Handle(GetChequeQuery request, CancellationToken cancellationToken) => Cheques.DetailAsync(db, caller, request.Id, cancellationToken);
}
