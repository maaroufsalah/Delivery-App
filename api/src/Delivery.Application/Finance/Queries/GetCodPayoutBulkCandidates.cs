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
using Delivery.Application.DeliveryNotes;
using Delivery.Application.Parcels;
using Delivery.Application.PettyCash;
using Delivery.Application.Returns;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Finance;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Finance;

/// <summary>`codPayoutBulkQuerySchema` : un bon par vendeur professionnel et par agence, à créer en masse.</summary>
public sealed record GetCodPayoutBulkCandidatesQuery(string? From, string? To, string? AgencyId) : IRequest<IReadOnlyList<CodPayoutBulkCandidateDto>>;

internal sealed class GetCodPayoutBulkCandidatesHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<GetCodPayoutBulkCandidatesQuery, IReadOnlyList<CodPayoutBulkCandidateDto>>
{
    public async Task<IReadOnlyList<CodPayoutBulkCandidateDto>> Handle(GetCodPayoutBulkCandidatesQuery request, CancellationToken cancellationToken) =>
        await CodPayouts.BulkCandidatesAsync(db, caller, request.From, request.To, request.AgencyId, cancellationToken);
}
