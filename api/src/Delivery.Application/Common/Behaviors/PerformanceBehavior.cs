using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Delivery.Application.Common.Behaviors;

/// <summary>Journalise tout cas d'usage qui dépasse 500 ms.</summary>
public sealed class PerformanceBehavior<TRequest, TResponse>(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    internal const int ThresholdMs = 500;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var watch = Stopwatch.StartNew();
        var response = await next(cancellationToken);
        watch.Stop();
        if (watch.ElapsedMilliseconds > ThresholdMs)
        {
            logger.LogWarning("Cas d'usage lent {UseCase} : {ElapsedMs} ms", typeof(TRequest).Name, watch.ElapsedMilliseconds);
        }
        return response;
    }
}
