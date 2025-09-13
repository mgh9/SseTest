using System.Threading.Channels;

namespace SseTest.Api;

public class OrchestratorSse
{
    private readonly string _sessionId;
    private readonly List<Provider> _providers;
    private readonly Channel<OrchestratorAvailabilities> _updateChannel;
    private bool _isStarted = false;

    public OrchestratorSse(string sessionId)
    {
        _sessionId = sessionId;
        _updateChannel = Channel.CreateUnbounded<OrchestratorAvailabilities>();

        _providers =
        [
            new Provider("Provider 1", 2, _updateChannel, true),
            new Provider("Provider 2", 5, _updateChannel, true),
            new Provider("Provider 3", 9, _updateChannel, true),
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

        // Start all providers asynchronously
        var providerTasks = _providers.Select(p => p.StartRetrievingFlightsFromSupplierAsync(cancellationToken)).ToArray();

        // Wait for all providers to complete
        await Task.WhenAll(providerTasks);

        // Send final update
        await SendUpdateAsync();

        // Close the channel
        _updateChannel.Writer.Complete();

        Console.WriteLine($"All providers completed for session {_sessionId}");
    }

    private async Task SendUpdateAsync()
    {
        var update = new OrchestratorAvailabilities
        {
            IsInProgress = !Cache.AreAllProvidersDone(_providers),
            Availabilities = Cache.GetAllResponses()
        };

        await _updateChannel.Writer.WriteAsync(update);
    }

    public async IAsyncEnumerable<OrchestratorAvailabilities> GetUpdatesAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var update in _updateChannel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return update;
        }
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

