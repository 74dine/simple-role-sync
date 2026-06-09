using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Server.Runners;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.Sources.Clear();

builder.Services.AddLogging(b =>
{
    b.SetMinimumLevel(LogLevel.Trace);

    b.ClearProviders();

    b.AddSimpleConsole(opt =>
    {
        opt.ColorBehavior = LoggerColorBehavior.Disabled;
        opt.SingleLine = true;
        opt.TimestampFormat = "[HH:mm:ss] ";
        opt.UseUtcTimestamp = true;
    });

    b.AddFilter("Microsoft.Hosting", LogLevel.Warning);
    b.AddFilter("Microsoft.Extensions.Hosting", LogLevel.Warning);
    b.AddFilter("System", LogLevel.Warning);
});

builder.Services.AddHostedService<DiscordBot>();

var host = builder.Build();

await host.RunAsync();
