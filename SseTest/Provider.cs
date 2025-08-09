using System.Threading.Channels;
using Bogus;
using SseTest.Api.Controllers;

namespace SseTest.Api;

public class Provider
{
    public string Name { get; }
    public int DelayInSeconds { get; }
    private readonly Cache _cache;
    private readonly Channel<OrchestratorAvailabilities> _updateChannel;

    public Provider(string name, int delayInSeconds, Cache cache, Channel<OrchestratorAvailabilities> updateChannel)
    {
        Name = name;
        DelayInSeconds = delayInSeconds;
        _cache = cache;
        _updateChannel = updateChannel;
    }

    public async Task StartRetrievingFlightsFromSupplier(CancellationToken cancellationToken)
    {
        Console.WriteLine($"Provider {Name} starting (will complete in {DelayInSeconds}s)");

        await Task.Delay(DelayInSeconds * 1000, cancellationToken);

        // Generate random data
        var faker = new Faker();
        int count = faker.Random.Int(0, 10);

        var records = new List<AvailabilityResponse>();
        for (int i = 0; i < count; i++)
        {
            var record = new AvailabilityResponse(
                Id: faker.Random.Int(1, 1000),
                ProviderName: Name,
                Price: faker.Random.Decimal(10, 1000),
                DateTime: faker.Date.Future(30)
            );
            records.Add(record);
        }

        _cache.AddResult(Name, records);

        // Send update after adding result
        await SendUpdateAsync();
    }

    private async Task SendUpdateAsync()
    {
        var update = new OrchestratorAvailabilities
        {
            IsInProgress = true, // Still in progress since this provider just finished
            Availabilities = _cache.GetAllResponses()
        };

        await _updateChannel.Writer.WriteAsync(update);
    }
}

