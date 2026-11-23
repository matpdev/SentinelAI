using System.Threading.Channels;
using Sentinel.Shared.Models;

namespace Sentinel.Client;

/// <summary>
/// Bounded in-memory queue so error reporting never blocks HTTP responses or exhausts memory during traffic spikes.
/// </summary>
public sealed class SentinelEventQueue(int capacity = 1000)
{
    private readonly Channel<IncidentEvent> _channel = Channel.CreateBounded<IncidentEvent>(
        new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    public bool TryEnqueue(IncidentEvent evt) => _channel.Writer.TryWrite(evt);

    public IAsyncEnumerable<IncidentEvent> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}
