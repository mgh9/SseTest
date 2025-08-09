using SseTest.Api.Controllers;

namespace SseTest.Api;

public class OrchestratorAvailabilities
{
    public int Count => Availabilities.Count;
    public bool IsInProgress { get; set; }
    public List<AvailabilityResponse> Availabilities { get; set; } = new();
}

