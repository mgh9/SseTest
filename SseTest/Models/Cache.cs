namespace SseTest.Api.Models;

public static class Cache
{
    private static readonly Dictionary<string, List<AvailabilityResponse>> _providersCache = [];
    private static readonly HashSet<string> _completedProviders = [];

    public static void AddResult(string providerName, List<AvailabilityResponse> responses)
    {
        _providersCache[providerName] = responses;
        _completedProviders.Add(providerName);
        Console.WriteLine($"Provider {providerName} completed with {responses.Count} responses");
    }

    public static List<AvailabilityResponse> GetAllResponses()
    {
        return _providersCache.Values.SelectMany(x => x).ToList();
    }

    public static bool AreAllProvidersDone(List<Provider> providers)
    {
        return providers.All(p => _completedProviders.Contains(p.Name));
    }

    public static void Clear()
    {
        _completedProviders.Clear();
        _providersCache.Clear();
    }
}

