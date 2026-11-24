using System.Text.Json;
using System.Threading.Channels;
using Delivery.Application.Common.Interfaces;
using Delivery.Domain.Common;
using Delivery.Domain.Entities;
using Delivery.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Application.BulkJobs;

/// <summary>La file en mémoire des tâches de masse ; reprise au démarrage depuis la base.</summary>
public sealed class BulkJobQueue
{
    private readonly Channel<string> _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(string jobId) => _channel.Writer.TryWrite(jobId);

    public IAsyncEnumerable<string> ReadAllAsync(CancellationToken cancellationToken) => _channel.Reader.ReadAllAsync(cancellationToken);
}
