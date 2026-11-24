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

public sealed record PreviewStatusCorrectionCommand : StatusCorrectionRequest, IRequest<StatusCorrectionPreviewDto>;

public sealed class PreviewStatusCorrectionValidator : StatusCorrectionValidator<PreviewStatusCorrectionCommand>;

internal sealed class PreviewStatusCorrectionHandler(IAppDbContext db, ICurrentUser caller) : IRequestHandler<PreviewStatusCorrectionCommand, StatusCorrectionPreviewDto>
{
    public Task<StatusCorrectionPreviewDto> Handle(PreviewStatusCorrectionCommand input, CancellationToken cancellationToken)
    {
        StatusCorrection.AssertAdmin(caller);
        return StatusCorrection.PreviewAsync(db, input.Code, input.ToInput(), cancellationToken);
    }
}
