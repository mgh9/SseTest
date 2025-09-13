namespace SseTest.Api.Services;

/************ it's better to have SSE contract[s] in the Application layer (and not the Domain layer)
 * but for now, we need this interface in `OrderAggregate` in the Domain layer
************/

public interface ISseNotifier<TEvent>
{
    /// <summary>
    /// Notifies all subscribers for a given key about a new event.
    /// </summary>
    /// <param name="key">The topic key (e.g., order ID, search ID).</param>
    /// <param name="eventName">The name of the event to send.</param>
    /// <param name="event">The event data object.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyAsync(string key, string eventName, TEvent @event, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes a client to receive notifications for a given key.
    /// </summary>
    /// <param name="key">The topic key to subscribe to.</param>
    /// <param name="responseStream">The client's response stream.</param>
    /// <param name="cancellationToken">A token that is cancelled when the client disconnects.</param>
    Task SubscribeAsync(string key, Stream responseStream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes the subscription for a given key and event name, cleaning up resources.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="eventName"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task CompleteAsync(string key, string eventName, CancellationToken cancellationToken = default);
}