using SseTest.Api.Models;

namespace SseTest.Api.Services;

public class OrchestratorPollingSample
{
    private readonly List<Provider> _providers;
    private static bool _isStarted = false;

    public OrchestratorPollingSample()
    {
        _providers =
        [
            new Provider("Provider 1", 2, null, false),
            new Provider("Provider 2", 5, null, false),
            new Provider("Provider 3", 9, null, false),
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

    public static void Reset()
    {
        _isStarted = false;
    }
}

