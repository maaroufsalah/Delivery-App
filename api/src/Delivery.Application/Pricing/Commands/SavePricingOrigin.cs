using System.Globalization;
using System.Text.RegularExpressions;
using AutoMapper;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Validation;
using Delivery.Application.Pricing.Dtos;
using Delivery.Application.Users.Dtos;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.Pricing.Commands;

/// <summary>`PUT /pricing-rules/by-origin/:originCityId` : enregistre la grille d'une origine.</summary>
public sealed record SavePricingOriginCommand : IRequest<PricingOriginSaveResult>
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string OriginCityId { get; init; } = string.Empty;

    public IReadOnlyList<PricingOriginRowInput>? Rows { get; init; }
}

public sealed class SavePricingOriginValidator : ZodValidator<SavePricingOriginCommand>
{
    public SavePricingOriginValidator()
    {
        RuleFor(x => x.Rows).Required("array").Must(r => r is null || r.Count >= 1).WithMessage("required").Must(r => r is null || r.Count <= 1000).WithMessage("too-many");
        RuleForEach(x => x.Rows).ChildRules(row =>
        {
            row.RuleFor(r => r.DestinationCityId).Required().MinLength(1, "required");
            row.RuleFor(r => r.Rate).Must((r, rate) => r.HasRate).WithMessage("required");
            row.RuleFor(r => r.Rate).MadAmount(nullable: true);
            row.RuleFor(r => r.DeliveryDelay).MaxLength(40, "too-long");
        });
    }
}

internal sealed class SavePricingOriginHandler(IAppDbContext db, IClock clock) : IRequestHandler<SavePricingOriginCommand, PricingOriginSaveResult>
{
    public Task<PricingOriginSaveResult> Handle(SavePricingOriginCommand input, CancellationToken cancellationToken) =>
        PricingGrid.SaveOriginAsync(db, clock, input.OriginCityId, input.Rows!, cancellationToken);
}
