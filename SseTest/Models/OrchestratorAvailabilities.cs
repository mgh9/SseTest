namespace SseTest.Api.Models;

public class OrchestratorAvailabilities
{
    public int Count => Availabilities.Count;
    public bool IsInProgress { get; set; }
    public List<AvailabilityResponse> Availabilities { get; set; } = [];
}

