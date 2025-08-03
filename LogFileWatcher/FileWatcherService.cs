using Microsoft.Extensions.Logging;

namespace LogFileWatcher;

public class WatchedFile(string fullPath)
{
    public string FullPath { get; } = fullPath;

    public string FileName { get; } = Path.GetFileName(fullPath);

    public Task? Task { get; set; }

    public CancellationTokenSource CancellationTokenSource { get; } = new();

    public SemaphoreSlim DataAvailableSemaphore { get; } = new(0);
}

public class FileWatcherService : IFileWatcherService
{
    private ILogger<FileWatcherService> _logger;

    private readonly List<WatchedFile> _watchedFiles = [];

    private readonly SemaphoreSlim _watchedFilesSemaphoreSlim = new(1, 1);

    public FileWatcherService(ILogger<FileWatcherService> logger)
    {
        _logger = logger;
    }

    public void WatchFile(string fullPath)
    {
        if (_watchedFiles.Any(w => w.FullPath == fullPath))
        {
            _logger.LogWarning("File {FullPath} already being watched.", fullPath);

            return;
        }

        _watchedFilesSemaphoreSlim.Wait();

        try
        {
            var watchedFile = new WatchedFile(fullPath);

            Console.WriteLine($"{watchedFile.FileName}:# created.");

            watchedFile.Task =
                Task.Run(async () => await WatchFileAsync(watchedFile, watchedFile.CancellationTokenSource.Token));

            _watchedFiles.Add(watchedFile);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception whilst trying to start watching file {FullPath}.", fullPath);
        }
        finally
        {
            _watchedFilesSemaphoreSlim.Release();
        }
    }

    public void FileChanged(string fullPath)
    {
        if (_watchedFiles.All(w => w.FullPath != fullPath))
        {
            WatchFile(fullPath);

            return;
        }

        _watchedFilesSemaphoreSlim.Wait();

        try
        {
            var watchedFile = _watchedFiles.Single(w => w.FullPath == fullPath);

            watchedFile.DataAvailableSemaphore.Release();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception whilst trying to inform file change {FullPath}.", fullPath);
        }
        finally
        {
            _watchedFilesSemaphoreSlim.Release();
        }
    }

    public void RemoveFile(string fullPath)
    {
        _ = Task.Run(() => RemoveFileAsync(fullPath));
    }

    private async Task RemoveFileAsync(string fullPath)
    {
        if (_watchedFiles.All(w => w.FullPath != fullPath))
        {
            return;
        }

        await _watchedFilesSemaphoreSlim.WaitAsync();

        try
        {
            var watchedFile = _watchedFiles.Single(w => w.FullPath == fullPath);

            await watchedFile.CancellationTokenSource.CancelAsync();

            try
            {
                if (watchedFile.Task != null)
                {
                    await watchedFile.Task;
                }
            }
            catch (Exception)
            {
                // ignored
            }

            Console.WriteLine($"{watchedFile.FileName}:# stopped watching.");

            _watchedFiles.Remove(watchedFile);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception whilst trying to remove file {FullPath}.", fullPath);
        }
        finally
        {
            _watchedFilesSemaphoreSlim.Release();
        }
    }

    private async Task WatchFileAsync(WatchedFile watchedFile, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var stream = File.Open(watchedFile.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            using var reader = new StreamReader(stream);

            long position = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                var fileInfo = new FileInfo(watchedFile.FullPath);

                if (position > fileInfo.Length)
                {
                    stream.Seek(0, SeekOrigin.Begin);
                }

                var line = await reader.ReadLineAsync(cancellationToken);

                position = stream.Position;

                if (line == null)
                {
                    await watchedFile.DataAvailableSemaphore.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);

                    if (!File.Exists(watchedFile.FullPath))
                    {
                        throw new Exception($"Watched file {watchedFile.FullPath} has been deleted.");
                    }

                    continue;
                }

                Console.WriteLine($"{watchedFile.FileName}: {line}");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"{watchedFile.FileName}:# exception: {e.GetType().FullName} {e.Message}");

            _ = Task.Run(() => RemoveFileAsync(watchedFile.FullPath));
        }
    }
}
