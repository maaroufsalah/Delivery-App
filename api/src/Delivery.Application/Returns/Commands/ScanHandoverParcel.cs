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
using Delivery.Application.Notifications;
using Delivery.Application.Parcels;
using Delivery.Application.Routes;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using Delivery.Domain.Parcels;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Returns;

/// <summary>`returnScanSchema` : un colis scanné (code ou référence).</summary>
public sealed record ScanHandoverParcelCommand : IRequest<ReturnHandoverDetailDto>
{
    [JsonIgnore]
    public string Handover { get; init; } = string.Empty;

    public string? Code { get; init => field = Zod.Trim(value); }
    public string? Note { get; init => field = Zod.Trim(value); }
}

public sealed class ScanHandoverParcelValidator : ZodValidator<ScanHandoverParcelCommand>
{
    public ScanHandoverParcelValidator()
    {
        RuleFor(x => x.Code).Required().MinLength(3, "too-short").MaxLength(64);
        RuleFor(x => x.Note).MaxLength(255);
    }
}

internal sealed class ScanHandoverParcelHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<ScanHandoverParcelCommand, ReturnHandoverDetailDto>
{
    public async Task<ReturnHandoverDetailDto> Handle(ScanHandoverParcelCommand input, CancellationToken cancellationToken)
    {
        await Handovers.LoadEditableAsync(db, caller, input.Handover, cancellationToken);
        var code = input.Code!.ToUpperInvariant();
        var parcel = await ParcelLookup.ResolveAsync(db.Parcels.AsNoTracking().Where(p => p.DeletedAt == null), code, async matched => await matched.Select(p => p.Id).FirstOrDefaultAsync(cancellationToken))
                     ?? throw BusinessException.NotFound($"Aucun colis ne porte le code {code}.", new { reason = "parcel-unknown", code });
        return await Handovers.AddParcelsAsync(db, caller, input.Handover, [parcel], null, null, null, input.Note, cancellationToken);
    }
}
