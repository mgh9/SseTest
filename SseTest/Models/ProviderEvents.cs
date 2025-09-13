namespace SseTest.Api.Models;

/// <summary>
/// Base event for provider-related events
/// </summary>
public abstract class ProviderEvent
{
    public string ProviderName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Event raised when a provider completes its data retrieval
/// </summary>
public class ProviderDataCompletedEvent : ProviderEvent
{
    public List<AvailabilityResponse> Data { get; set; } = [];
}

/// <summary>
/// Event raised when a provider starts its data retrieval
/// </summary>
public class ProviderStartedEvent : ProviderEvent
{
    public int ExpectedDelayInSeconds { get; set; }
}

/// <summary>
/// Event raised when a provider encounters an error
/// </summary>
public class ProviderErrorEvent : ProviderEvent
{
    public string ErrorMessage { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
}
