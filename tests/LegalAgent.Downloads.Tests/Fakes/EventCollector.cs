using LegalAgent.Downloads.Model;

namespace LegalAgent.Downloads.Tests.Fakes;

/// <summary>Synchronous, thread-safe progress sink.</summary>
internal sealed class EventCollector : IProgress<DownloadEvent>
{
    private readonly List<DownloadEvent> events = [];

    /// <summary>Events in report order.</summary>
    public IReadOnlyList<DownloadEvent> Events
    {
        get
        {
            lock (events)
            {
                return [.. events];
            }
        }
    }

    /// <inheritdoc />
    public void Report(DownloadEvent value)
    {
        lock (events)
        {
            events.Add(value);
        }
    }
}
