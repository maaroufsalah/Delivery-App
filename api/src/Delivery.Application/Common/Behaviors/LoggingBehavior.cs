using MediatR;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Common.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;
        logger.LogDebug("Cas d'usage {UseCase} : début", name);
        var response = await next(cancellationToken);
        logger.LogDebug("Cas d'usage {UseCase} : fin", name);
        return response;
    }
}
