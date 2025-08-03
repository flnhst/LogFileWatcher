using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LogFileWatcher;

public class LogFileWatcherConfiguration(string path, string filter)
{
    public string Path { get; set; } = path;

    public string Filter { get; set; } = filter;
}

public class FileSystemWatcherHostedService : IHostedService, IDisposable
{
    private readonly ILogger<FileSystemWatcherHostedService> _logger;
    private readonly IList<string> _commandLineArguments;
    private readonly IFileWatcherService _fileWatcherService;

    private LogFileWatcherConfiguration? _configuration;
    private FileSystemWatcher? _watcher;

    public FileSystemWatcherHostedService(ILogger<FileSystemWatcherHostedService> logger, [FromKeyedServices(ServiceKeys.CommandLineArguments)] IList<string> commandLineArguments, IFileWatcherService fileWatcherService)
    {
        _logger = logger;
        _commandLineArguments = commandLineArguments;
        _fileWatcherService = fileWatcherService;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _configuration = ProcessArguments();

        /*
        var files = Directory.EnumerateFiles(_configuration.Path, _configuration.Filter, new EnumerationOptions()
        {
            RecurseSubdirectories = true
        });

        foreach (var file in files)
        {
            _fileWatcherService.WatchFile(Path.GetFullPath(file), true);
        }
        */

        _watcher = new FileSystemWatcher(_configuration.Path, _configuration.Filter);

        _watcher.NotifyFilter = NotifyFilters.CreationTime
                                | NotifyFilters.FileName
                                | NotifyFilters.Size;
        _watcher.IncludeSubdirectories = true;
        _watcher.EnableRaisingEvents = true;

        _watcher.Changed += WatcherOnChanged;
        _watcher.Created += WatcherOnCreated;
        _watcher.Deleted += WatcherOnDeleted;
        _watcher.Renamed += WatcherOnRenamed;
        _watcher.Error += WatcherOnError;

        _logger.LogInformation("Started file system watcher.");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_watcher == null)
        {
            return Task.CompletedTask;
        }

        _watcher.EnableRaisingEvents = false;

        _logger.LogInformation("Stopped file system watcher.");

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _watcher?.Dispose();

        GC.SuppressFinalize(this);
    }

    private LogFileWatcherConfiguration ProcessArguments()
    {
        if (_commandLineArguments.Count != 2)
        {
            throw new Exception("Invalid command line arguments.");
        }

        var configuration = new LogFileWatcherConfiguration(_commandLineArguments[0], _commandLineArguments[1]);

        if (!Path.Exists(configuration.Path))
        {
            throw new Exception($"Path does not exist: {configuration.Path}");
        }

        return configuration;
    }

    private void WatcherOnChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            if (e.ChangeType == WatcherChangeTypes.Changed)
            {
                _logger.LogInformation("File changed: {FullPath} {ChangeType}", e.FullPath, e.ChangeType);

                _fileWatcherService.FileChanged(Path.GetFullPath(e.FullPath));
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error processing change event for file: {FullPath}", e.FullPath);
        }
    }

    private void WatcherOnCreated(object sender, FileSystemEventArgs e)
    {
        try
        {
            _logger.LogInformation("File created: {FullPath}", e.FullPath);

            _fileWatcherService.WatchFile(Path.GetFullPath(e.FullPath));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error processing create event for file: {FullPath}", e.FullPath);
        }
    }

    private void WatcherOnDeleted(object sender, FileSystemEventArgs e)
    {
        try
        {
            _logger.LogInformation("File deleted: {FullPath}", e.FullPath);

            _fileWatcherService.RemoveFile(Path.GetFullPath(e.FullPath));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error processing delete event for file: {FullPath}", e.FullPath);
        }
    }

    private void WatcherOnRenamed(object sender, RenamedEventArgs e)
    {
        try
        {
            _logger.LogInformation("File renamed: {FullPath}", e.FullPath);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error processing rename event for file: {FullPath}", e.FullPath);
        }
    }

    private void WatcherOnError(object sender, ErrorEventArgs e)
    {
        try
        {
            _logger.LogError(e.GetException(), "Error happened during file watching.");
        }
        catch (Exception exception)
        {
            _logger.LogError(new AggregateException(exception, e.GetException()), "Error processing error event.");
        }
    }
}
