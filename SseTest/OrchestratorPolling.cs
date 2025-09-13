using System.Threading.Channels;

namespace SseTest.Api;

public class OrchestratorPolling
{
    private readonly List<Provider> _providers;
    private readonly Channel<OrchestratorAvailabilities> _updateChannel;
    private bool _isStarted = false;

    public OrchestratorPolling()
    {
        _updateChannel = Channel.CreateUnbounded<OrchestratorAvailabilities>();

        _providers =
        [
            new Provider("Provider 1", 2, _updateChannel, false),
            new Provider("Provider 2", 5, _updateChannel, false),
            new Provider("Provider 3", 9, _updateChannel, false),
        ];
    }

    public async Task<OrchestratorAvailabilities> GetDataAsync(CancellationToken cancellationToken)
    {
        if (_isStarted)
        {
            return new OrchestratorAvailabilities
            {
                IsInProgress = !Cache.AreAllProvidersDone(_providers),
                Availabilities = Cache.GetAllResponses()
            };
        }

        _isStarted = true;
        Console.WriteLine($"Starting orchestrator...");

        // Start all providers asynchronously
        await Task.Run(() =>
        {
            _providers.ForEach(async p => await p.StartRetrievingFlightsFromSupplierAsync(cancellationToken));
        }, cancellationToken);

        Console.WriteLine($"Sending to all providers completed");

        return new OrchestratorAvailabilities
        {
            IsInProgress = !Cache.AreAllProvidersDone(_providers),
            Availabilities = Cache.GetAllResponses()
        };
    }
}

