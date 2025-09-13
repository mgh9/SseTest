using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace SseTest.Api.Services;

/// <summary>
/// An in-memory implementation of the ISseNotifier interface for server-sent events (SSE).
/// *** for distributed systems in future, consider using a distributed message broker like Redis PUB/SUB or RabbitMQ ***
/// </summary>
/// <typeparam name="TEvent"></typeparam>
public sealed class InMemorySseNotifier<TEvent>(ILogger<InMemorySseNotifier<TEvent>> logger) : ISseNotifier<TEvent>, IDisposable
{
    private class Subscriber
    {
        // Each subscriber has its own channel to receive events
        public Channel<(string eventName, string jsonData)> Channel { get; } =
            System.Threading.Channels.Channel.CreateUnbounded<(string, string)>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    }

    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Subscriber>> _subscriptions = new();
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly int _maxSubscribersPerKey = 10; // Limit subscribers per key

    /// <summary>
    /// This method subscribes a client to server-sent events (SSE) for a specific key.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="responseStream"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task SubscribeAsync(string key, Stream responseStream, CancellationToken cancellationToken = default)
    {
        // Check subscriber cap per key
        var bucket = _subscriptions.GetOrAdd(key, _ => new ConcurrentDictionary<Guid, Subscriber>());
        if (bucket.Count >= _maxSubscribersPerKey)
        {
            logger.LogWarning("Max subscribers reached for key {Key}", key);
            throw new InvalidOperationException($"Too many subscribers for key {key}");
        }

        // Create a new subscriber and add it to the dictionary
        var id = Guid.NewGuid();
        logger.LogInformation("SSE subscriber {SubscriberId} connected for key {Key}", id, key);

        var subscriber = new Subscriber();
        bucket.TryAdd(id, subscriber);

        // Absolute global timeout to auto-cleanup long-lived streams
        using var lifetimeCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lifetimeCancellationToken.CancelAfter(TimeSpan.FromMinutes(10)); // configurable

        // Set the response headers for SSE
        await using var writer = new StreamWriter(responseStream, Encoding.UTF8, 1024, leaveOpen: true);

        try
        {
            // Initial connected message (comment so it's ignored by browser, visible in Postman)
            await writer.WriteAsync($":connected {DateTime.UtcNow:O}\n\n".AsMemory(), lifetimeCancellationToken.Token);
            await writer.FlushAsync(lifetimeCancellationToken.Token);

            logger.LogDebug("Initial connected message sent to subscriber {SubscriberId} for key {Key}", id, key);

            // Start pumping events from the channel to the response stream
            var pumpTask = PumpChannelToResponseAsync(subscriber.Channel.Reader, writer, id, key, lifetimeCancellationToken.Token);

            // Start the heartbeat task to keep the connection alive
            var heartbeatTask = SendHeartbeatAsync(writer, id,key, lifetimeCancellationToken.Token);

            // Wait for either the pump task or the heartbeat task to complete (which happens on cancellation)
            // Both loops respect cancellation & heartbeat ensures TCP liveness
            await Task.WhenAny(pumpTask, heartbeatTask);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SSE subscription {SubscriberId} for key {Key} failed", id, key);
            throw;
        }
        finally
        {
            // Log + cleanup on disconnect
            if (_subscriptions.TryGetValue(key, out var dict))
            {
                // Remove the subscriber from the dictionary
                dict.TryRemove(id, out _);
                if (dict.IsEmpty)
                    _subscriptions.TryRemove(key, out _);
            }

            logger.LogInformation("SSE subscriber {SubscriberId} disconnected (Stream closed) for key {Key}", id, key);
        }
    }

    private async Task PumpChannelToResponseAsync(ChannelReader<(string eventName, string jsonData)> reader, StreamWriter writer, Guid subscriberId, string key, CancellationToken cancellationToken)
    {
        await foreach (var (eventName, jsonData) in reader.ReadAllAsync(cancellationToken))
        {
            if (!string.IsNullOrEmpty(eventName))
                await writer.WriteAsync($"event: {eventName}\n".AsMemory(), cancellationToken);

            if (jsonData is not null)
                await writer.WriteAsync($"data: {jsonData}\n\n".AsMemory(), cancellationToken);

            /*
             a sample structure:

            event: OrderStatusUpdate
            data: {"orderId":"123","reserveStep":"PaidBack","isSuccess":false,...}
            
             */

            // Ensure the data is sent immediately
            await writer.FlushAsync(cancellationToken);

            logger.LogDebug("Sent event '{EventName}' to subscriber {SubscriberId} for key {Key}", eventName, subscriberId, key);
        }

        logger.LogInformation("Pump finished for subscriber {SubscriberId} on key {Key}", subscriberId, key);
    }

    private static async Task SendHeartbeatAsync(StreamWriter writer, Guid id, string key, CancellationToken cancellationToken)
    {
        // Send periodic heartbeats to keep the connection alive
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Wait for 15 seconds between heartbeats
                await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);

                var heartbeatPayloadJson = JsonSerializer.Serialize(new { type = "heartbeat", timestamp = DateTime.UtcNow.ToString("O") });

                // Send a comment line as a heartbeat to keep the connection alive
                // (comments in SSE start with a colon)
                // the heartbeat and connected messages use a colon (:) at the beginning
                await writer.WriteLineAsync("event: heartbeat");
                await writer.WriteLineAsync($"data: {heartbeatPayloadJson}");
                await writer.WriteLineAsync(); // blank line ends the SSE message

                // Ensure the heartbeat is sent immediately
                await writer.FlushAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Notifies all subscribers for a given key about a new event.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="eventName"></param>
    /// <param name="event"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task NotifyAsync(string key, string eventName, TEvent @event, CancellationToken cancellationToken = default)
    {
        if (@event == null) return Task.CompletedTask;

        try
        {
            var json = JsonSerializer.Serialize(@event, _jsonOptions);

            // If there are subscribers for the key, send the event to each
            if (_subscriptions.TryGetValue(key, out var dict))
            {
                // Notify all subscribers in parallel
                foreach (var kv in dict)
                {
                    // Write the tuple to the channel
                    // The PumpChannelToResponseAsync method will read from this channel and send to the client
                    kv.Value.Channel.Writer.TryWrite((eventName, json));
                }
            }

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            // Log the error instead of crashing
            logger.LogError(ex, "Error serializing event '{EventName}' for key {Key}", eventName, key);
            return Task.CompletedTask;
        }
    }

    public async Task CompleteAsync(string key, string eventName, CancellationToken cancellationToken = default)
    {
        if (_subscriptions.TryGetValue(key, out var subscribers))
        {
            foreach (var sub in subscribers.Values)
            {
                var payload = new { status = "finished" };
                await sub.Channel.Writer.WriteAsync((eventName, JsonSerializer.Serialize(payload)), cancellationToken);

                // Signal the end of the stream
                sub.Channel.Writer.Complete();

                logger.LogInformation("Completed SSE stream for key {Key}", key);
            }
        }
    }

    public void Dispose()
    {
        // Complete all channels to signal subscribers to stop
        foreach (var bucket in _subscriptions.Values)
        {
            foreach (var sub in bucket.Values)
            {
                sub.Channel.Writer.TryComplete();
            }
        }

        // Clear all subscriptions
        _subscriptions.Clear();
    }
}