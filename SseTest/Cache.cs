using SseTest.Api.Controllers;

namespace SseTest.Api;

public class Cache
{
    private readonly Dictionary<string, List<AvailabilityResponse>> _providersCache = [];
    private readonly HashSet<string> _completedProviders = [];

    public void AddResult(string providerName, List<AvailabilityResponse> responses)
    {
        _providersCache[providerName] = responses;
        _completedProviders.Add(providerName);
        Console.WriteLine($"Provider {providerName} completed with {responses.Count} responses");
    }

    public List<AvailabilityResponse> GetAllResponses()
    {
        return _providersCache.Values.SelectMany(x => x).ToList();
    }

    public bool AreAllProvidersDone(List<Provider> providers)
    {
        return providers.All(p => _completedProviders.Contains(p.Name));
    }
}

