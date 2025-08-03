using Microsoft.Extensions.DependencyInjection;
using LogFileWatcher;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddLogging(loggingBuilder =>
{
    if (Environment.GetEnvironmentVariable("LFW_LOGGING") != "1")
    {
        loggingBuilder
            .ClearProviders();
    }
});

builder.Services.AddHostedService<FileSystemWatcherHostedService>();
builder.Services.AddKeyedSingleton<IList<string>>(ServiceKeys.CommandLineArguments, args.ToList());
builder.Services.AddSingleton<IFileWatcherService, FileWatcherService>();

using var host = builder.Build();

await host.RunAsync();
