using System.Threading.Channels;
using SseTest.Api.Models;

namespace SseTest.Api.Services;

public class OrchestratorPollingSample
{
    private readonly List<Provider> _providers;
    private readonly Channel<OrchestratorAvailabilities> _updateChannel;
    private bool _isStarted = false;

    public OrchestratorPollingSample()
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

