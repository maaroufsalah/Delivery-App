using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Documents;
using Delivery.Application.Common.Export;
using Delivery.Application.Common.Idempotency;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Models;
using Delivery.Application.Common.Security;
using Delivery.Application.Common.Settings;
using Delivery.Application.Common.Validation;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.PettyCash;

internal static class ProofRules
{
    public static readonly string[] Types = ["application/pdf", "image/jpeg", "image/png", "image/webp"];

    public static void Proof<T>(ZodValidator<T> validator, System.Linq.Expressions.Expression<Func<T, ProofFileDto?>> proof)
    {
        var get = proof.Compile();
        validator.RuleFor(x => get(x)!.Name).Required().MinLength(1, "required").MaxLength(120).OverridePropertyName("proof.name").When(x => get(x) is not null);
        validator.RuleFor(x => get(x)!.MimeType).Must(v => v is not null && Types.Contains(v)).WithMessage("unsupported-file-type").OverridePropertyName("proof.mimeType").When(x => get(x) is not null);
        validator.RuleFor(x => get(x)!.Content).Required().MinLength(1, "required").OverridePropertyName("proof.content").When(x => get(x) is not null);
    }

    /// <summary>`madAmount` (petite caisse) : requis, strictement positif, 1 000 000 MAD au plus.</summary>
    public static void Amount<T>(ZodValidator<T> validator, System.Linq.Expressions.Expression<Func<T, double?>> amount)
    {
        validator.RuleFor(amount).Must(v => v is not null).WithMessage("required").Must(v => v is null || v.Value > 0).WithMessage("must-be-positive")
            .Must(v => v is null || v.Value <= 1_000_000).WithMessage("amount-too-large");
    }
}
