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

/// <summary>`DELETE /pricing-rules/:id` : suppression douce, la règle est désactivée.</summary>
public sealed record RemovePricingRuleCommand(string Id) : IRequest<IdResult>;

internal sealed class RemovePricingRuleHandler(IAppDbContext db, IClock clock) : IRequestHandler<RemovePricingRuleCommand, IdResult>
{
    public async Task<IdResult> Handle(RemovePricingRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await db.PricingRules.FirstOrDefaultAsync(r => r.Id == request.Id && r.DeletedAt == null, cancellationToken)
            ?? throw BusinessException.NotFound("Tarif introuvable.");
        rule.DeletedAt = clock.UtcNow;
        rule.IsEnabled = false;
        await db.SaveChangesAsync(cancellationToken);
        return new IdResult(request.Id);
    }
}
