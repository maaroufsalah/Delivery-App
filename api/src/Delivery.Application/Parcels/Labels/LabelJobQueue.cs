using System.Threading.Channels;

namespace Delivery.Application.Parcels.Labels;

/// <summary>Un travail d'étiquettes en attente : les codes déjà résolus dans la portée du demandeur.</summary>
public sealed record LabelJobWork(string JobId, IReadOnlyList<string> Codes, string Format, string Folder);

/// <summary>
/// La file en mémoire de `LabelJobsService` : un seul rendu à la fois, dans l'ordre
/// d'arrivée. Un redémarrage la vide ; le ménage du démarrage passe alors les
/// travaux restés en cours à « échoué » (`interrupted`), comme Speedy.
/// </summary>
public sealed class LabelJobQueue
{
    private readonly Channel<LabelJobWork> _channel = Channel.CreateUnbounded<LabelJobWork>(new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(LabelJobWork work) => _channel.Writer.TryWrite(work);

    public IAsyncEnumerable<LabelJobWork> ReadAllAsync(CancellationToken cancellationToken) => _channel.Reader.ReadAllAsync(cancellationToken);
}
