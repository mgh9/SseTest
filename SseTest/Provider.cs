using System.Threading.Channels;
using Bogus;
using SseTest.Api.Controllers;

namespace SseTest.Api;

public class Provider
{
    public string Name { get; }
    public int DelayInSeconds { get; }
    private readonly Channel<OrchestratorAvailabilities> _updateChannel;
    private readonly bool _sendUpdate;

    public Provider(string name, int delayInSeconds, Channel<OrchestratorAvailabilities> updateChannel, bool sendUpdate)
    {
        Name = name;
        DelayInSeconds = delayInSeconds;
        _updateChannel = updateChannel;
        _sendUpdate = sendUpdate;
    }

    public async Task StartRetrievingFlightsFromSupplierAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"Provider {Name} starting (will complete in {DelayInSeconds}s)");

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

        // Send update after adding result
        if(_sendUpdate)
            await SendUpdateAsync();
    }

    private async Task SendUpdateAsync()
    {
        var update = new OrchestratorAvailabilities
        {
            IsInProgress = true, // Still in progress since this provider just finished
            Availabilities = Cache.GetAllResponses()
        };

        await _updateChannel.Writer.WriteAsync(update);
    }
}

