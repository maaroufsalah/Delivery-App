using System.Text.Json;
using System.Text.Json.Serialization;
using Delivery.Application.Common.Behaviors;
using Delivery.Application.Common.Interfaces;
using Delivery.Application.Common.Validation;
using Delivery.Application.Parcels.Commands;
using Delivery.Domain.Common;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Routes;

public sealed record ApplyStatusCorrectionCommand : StatusCorrectionRequest, IRequest<StatusCorrectionResultDto>;

public sealed class ApplyStatusCorrectionValidator : StatusCorrectionValidator<ApplyStatusCorrectionCommand>;

internal sealed class ApplyStatusCorrectionHandler(IAppDbContext db, ICurrentUser caller, IClock clock, ILogger<ApplyStatusCorrectionHandler> logger)
    : IRequestHandler<ApplyStatusCorrectionCommand, StatusCorrectionResultDto>
{
    public Task<StatusCorrectionResultDto> Handle(ApplyStatusCorrectionCommand input, CancellationToken cancellationToken)
    {
        StatusCorrection.AssertAdmin(caller);
        return StatusCorrection.ApplyAsync(db, caller, clock, logger, input.Code, input.ToInput(), cancellationToken);
    }
}
