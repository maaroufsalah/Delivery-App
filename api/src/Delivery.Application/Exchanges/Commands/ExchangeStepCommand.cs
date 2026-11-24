using System.Text.Json.Serialization;
using Delivery.Application.Common.Validation;
using Delivery.Application.Exchanges.Dtos;
using FluentValidation;
using MediatR;

namespace Delivery.Application.Exchanges.Commands;

/// <summary>`exchangeAdvanceSchema` : un commentaire facultatif.</summary>
public abstract record ExchangeStepCommand : IRequest<ExchangeDetailDto>
{
    [JsonIgnore]
    public string Code { get; init; } = string.Empty;

    public string? Comment { get; init => field = Zod.Trim(value); }
}

public abstract class ExchangeStepValidator<T> : ZodValidator<T>
    where T : ExchangeStepCommand
{
    protected ExchangeStepValidator()
    {
        RuleFor(x => x.Comment).MaxLength(500);
    }
}
