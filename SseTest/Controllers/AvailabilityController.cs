using Microsoft.AspNetCore.Mvc;
using SseTest.Api.Models;
using SseTest.Api.Services;

namespace SseTest.Api.Controllers;

[ApiController]
[Route("[controller]")]
public partial class AvailabilityController(ISseNotifier<OrchestratorAvailabilities> sseNotifier) : ControllerBase
{
    [HttpGet("stream")]
    public async Task Stream(CancellationToken cancellationToken)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        // Create a unique session for this client
        var sessionId = Guid.NewGuid().ToString();

        // Create a new event channel for this session
        var eventChannel = new ProviderEventChannel();
        var orchestrator = new OrchestratorSseSample(sessionId, sseNotifier, eventChannel);

        Console.WriteLine($"Starting SSE stream for session {sessionId}");

        try
        {
            // Start the orchestrator in background
            var orchestratorTask = Task.Run(async () => await orchestrator.StartAsync(cancellationToken), cancellationToken);

            // Subscribe to SSE updates using the notifier
            await sseNotifier.SubscribeAsync(sessionId, Response.Body, cancellationToken);
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

    [HttpDelete("cache")]
    public async Task ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        Cache.Clear();
        OrchestratorPollingSample.Reset();
        await Task.CompletedTask;
    }
}

