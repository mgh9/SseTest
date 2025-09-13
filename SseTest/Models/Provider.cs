using Bogus;
using SseTest.Api.Services;

namespace SseTest.Api.Models;

public class Provider(string name, int delayInSeconds, ProviderEventChannel? eventChannel, bool sendUpdate)
{
    public string Name { get; } = name;
    public int DelayInSeconds { get; } = delayInSeconds;
    private readonly ProviderEventChannel? _eventChannel = eventChannel;

    public async Task StartRetrievingFlightsFromSupplierAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"Provider {Name} starting (will complete in {DelayInSeconds}s)");

        // Publish provider started event (if event channel is available)
        if (_eventChannel != null)
        {
            await _eventChannel.PublishAsync(new ProviderStartedEvent
            {
                ProviderName = Name,
                ExpectedDelayInSeconds = DelayInSeconds
            }, cancellationToken);
        }

        try
        {
            await Task.Delay(DelayInSeconds * 1000, cancellationToken);

            // Generate random data
            var faker = new Faker();
            int count = faker.Random.Int(0, 10);

            var data = new List<AvailabilityResponse>();
            for (int i = 0; i < count; i++)
            {
                var record = new AvailabilityResponse(
                    Id: faker.Random.Int(1, 1000),
                    ProviderName: Name,
                    Price: faker.Random.Decimal(10, 1000),
                    DateTime: faker.Date.Future(30)
                );
                data.Add(record);
            }

            Cache.AddResult(Name, data);

            // Publish provider completed event (if event channel is available)
            if (sendUpdate && _eventChannel != null)
            {
                await _eventChannel.PublishAsync(new ProviderDataCompletedEvent
                {
                    ProviderName = Name,
                    Data = data
                }, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // Publish provider error event (if event channel is available)
            if (_eventChannel != null)
            {
                await _eventChannel.PublishAsync(new ProviderErrorEvent
                {
                    ProviderName = Name,
                    ErrorMessage = ex.Message,
                    Exception = ex
                }, cancellationToken);
            }
        }
    }
}

