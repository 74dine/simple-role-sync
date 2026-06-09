using Microsoft.Extensions.Hosting;

namespace Server.Runners;

public class DiscordBot : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Hello, World!");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Goodbye, World!");
        return Task.CompletedTask;
    }
}
