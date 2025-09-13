using Microsoft.AspNetCore.Mvc;
using SseTest.Api.Services;

namespace SseTest.Api.Controllers;

public partial class AvailabilityController
{
    private static readonly OrchestratorPollingSample _pollingOrchestrator = new();

    [HttpGet("polling")]
    public async Task<IActionResult> PollingAsync()
    {
        var availabilities = await _pollingOrchestrator.GetDataAsync(CancellationToken.None);
        return Ok(availabilities);
    }
}
