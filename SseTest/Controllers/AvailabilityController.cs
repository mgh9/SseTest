using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace SseTest.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class AvailabilityController : ControllerBase
{
    [HttpPost("clear")]
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        Cache.Clear();
        await Task.CompletedTask;
    }

    [HttpGet("stream")]
    public async Task Stream(CancellationToken cancellationToken)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        // Create a unique session for this client
        var sessionId = Guid.NewGuid().ToString();
        var orchestrator = new OrchestratorSse(sessionId);

        Console.WriteLine($"Starting SSE stream for session {sessionId}");

        try
        {
            // Start the orchestrator in background
            var orchestratorTask = Task.Run(async () => await orchestrator.StartAsync(cancellationToken), cancellationToken);

            // Subscribe to data updates
            await foreach (var update in orchestrator.GetUpdatesAsync(cancellationToken))
            {
                var availabilityJson = JsonSerializer.Serialize(update);
                Console.WriteLine($"data >>> {availabilityJson}");

                await Response.WriteAsync($"data: {availabilityJson}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);

                // If all providers are done, end the stream
                if (!update.IsInProgress)
                {
                    Console.WriteLine("All providers completed - ending stream");
                    break;
                }
            }
        }
        catch (OperationCanceledException ex)
        {
            Console.WriteLine("Operation cancelled : " + ex.Message);
        }
        finally
        {
            Console.WriteLine($"SSE stream ended for session {sessionId}");
        }
    }

    private static readonly OrchestratorPolling _pollingOrchestrator = new();
    [HttpGet("polling")]
    public async Task<IActionResult> PollingAsync()
    {
        var availabilities = await _pollingOrchestrator.GetDataAsync(CancellationToken.None);
        return Ok(availabilities);
    }
}
