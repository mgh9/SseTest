using System.Threading.Channels;
using SseTest.Api.Models;

namespace SseTest.Api.Services;

/// <summary>
/// Manages the event channel for provider events
/// This simulates a message broker like RabbitMQ in a real distributed system
/// </summary>
public class ProviderEventChannel
{
    private readonly Channel<ProviderEvent> _eventChannel;
    private readonly ChannelWriter<ProviderEvent> _writer;
    private readonly ChannelReader<ProviderEvent> _reader;

    public ProviderEventChannel()
    {
        _eventChannel = Channel.CreateUnbounded<ProviderEvent>();
        _writer = _eventChannel.Writer;
        _reader = _eventChannel.Reader;
    }

    /// <summary>
    /// Publishes a provider event to the channel
    /// </summary>
    public async Task PublishAsync(ProviderEvent providerEvent, CancellationToken cancellationToken = default)
    {
        await _writer.WriteAsync(providerEvent, cancellationToken);
    }

    /// <summary>
    /// Subscribes to provider events
    /// </summary>
    public IAsyncEnumerable<ProviderEvent> SubscribeAsync(CancellationToken cancellationToken = default)
    {
        return _reader.ReadAllAsync(cancellationToken);
    }

    /// <summary>
    /// Completes the event channel
    /// </summary>
    public void Complete()
    {
        _writer.Complete();
    }
}
