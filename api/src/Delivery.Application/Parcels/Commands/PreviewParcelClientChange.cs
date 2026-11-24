using System.Text.Json.Serialization;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Application.Notifications;
using Delivery.Domain.Common;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Parcels.Commands;

/// <summary>`POST /parcels/:code/client/preview` : l'aperçu du changement de client.</summary>
public sealed record PreviewParcelClientChangeCommand : IRequest<ParcelClientChangePreviewDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public string? ClientId { get; init => field = Zod.Trim(value); }
}

public sealed class PreviewParcelClientChangeValidator : ZodValidator<PreviewParcelClientChangeCommand>
{
    public PreviewParcelClientChangeValidator()
    {
        RuleFor(x => x.ClientId).Required().MinLength(1, "required");
    }
}

internal sealed class PreviewParcelClientChangeHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<PreviewParcelClientChangeCommand, ParcelClientChangePreviewDto>
{
    public async Task<ParcelClientChangePreviewDto> Handle(PreviewParcelClientChangeCommand input, CancellationToken cancellationToken)
    {
        ParcelAdmin.AssertAdmin(caller);
        return (await ParcelAdmin.PlanAsync(db, input.Code, input.ClientId!, cancellationToken)).Preview;
    }
}
