using SseTest.Api.Models;

namespace SseTest.Api.Services;

public class OrchestratorSseSample
{
    private readonly string _sessionId;
    private readonly List<Provider> _providers;
    private readonly ISseNotifier<OrchestratorAvailabilities> _sseNotifier;
    private readonly ProviderEventChannel _eventChannel;
    private bool _isStarted = false;
    private readonly HashSet<string> _completedProviders = [];

    public OrchestratorSseSample(string sessionId, ISseNotifier<OrchestratorAvailabilities> sseNotifier, ProviderEventChannel eventChannel)
    {
        _sessionId = sessionId;
        _sseNotifier = sseNotifier;
        _eventChannel = eventChannel;

        _providers =
        [
            new Provider("Provider 1", 2, _eventChannel, true),
            new Provider("Provider 2", 5, _eventChannel, true),
            new Provider("Provider 3", 9, _eventChannel, true),
        ];

        Cache.Clear();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_isStarted) return;
        _isStarted = true;

        Console.WriteLine($"Starting orchestrator for session {_sessionId}");

        // Send initial state
        await SendUpdateAsync();

        // Start event processing task (runs concurrently)
        var eventProcessingTask = Task.Run(async () => await ProcessProviderEventsAsync(cancellationToken), cancellationToken);

        // Start all providers asynchronously
        var providerTasks = _providers.Select(p => p.StartRetrievingFlightsFromSupplierAsync(cancellationToken)).ToArray();

        // Wait for all providers to complete
        await Task.WhenAll(providerTasks);

        // Complete the event channel to signal no more events
        _eventChannel.Complete();

        // Wait for event processing to finish (this will process all remaining events)
        await eventProcessingTask;

        // Complete the SSE stream
        await _sseNotifier.CompleteAsync(_sessionId, "availability-update", cancellationToken);

        Console.WriteLine($"All providers completed for session {_sessionId}");
    }

    private async Task ProcessProviderEventsAsync(CancellationToken cancellationToken)
    {
        try
        {
            Console.WriteLine($"Event processing started for session {_sessionId}");

            await foreach (var providerEvent in _eventChannel.SubscribeAsync(cancellationToken))
            {
                Console.WriteLine($"Orchestrator received event: {providerEvent.GetType().Name} from {providerEvent.ProviderName}");

                switch (providerEvent)
                {
                    case ProviderStartedEvent startedEvent:
                        Console.WriteLine($"Provider {startedEvent.ProviderName} started (expected delay: {startedEvent.ExpectedDelayInSeconds}s)");
                        break;

                    case ProviderDataCompletedEvent completedEvent:
                        Console.WriteLine($"Provider {completedEvent.ProviderName} completed with {completedEvent.Data.Count} results");
                        _completedProviders.Add(completedEvent.ProviderName);
                        Console.WriteLine($"Sending SSE update - Completed providers: {_completedProviders.Count}/{_providers.Count}");
                        await SendUpdateAsync();
                        break;

                    case ProviderErrorEvent errorEvent:
                        Console.WriteLine($"Provider {errorEvent.ProviderName} failed: {errorEvent.ErrorMessage}");
                        _completedProviders.Add(errorEvent.ProviderName);
                        Console.WriteLine($"Sending SSE update after error - Completed providers: {_completedProviders.Count}/{_providers.Count}");
                        await SendUpdateAsync();
                        break;
                }
            }

            Console.WriteLine($"Event processing completed for session {_sessionId}");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Event processing cancelled");
        }
    }

    private async Task SendUpdateAsync()
    {
        var update = new OrchestratorAvailabilities
        {
            IsInProgress = _completedProviders.Count < _providers.Count,
            Availabilities = Cache.GetAllResponses()
        };

        Console.WriteLine($"Sending SSE update: IsInProgress={update.IsInProgress}, Count={update.Count}");
        await _sseNotifier.NotifyAsync(_sessionId, "availability-update", update);
    }

    public OrchestratorAvailabilities GetCurrentSnapshot()
    {
        return new OrchestratorAvailabilities
        {
            IsInProgress = !Cache.AreAllProvidersDone(_providers),
            Availabilities = Cache.GetAllResponses()
        };
    }
}

