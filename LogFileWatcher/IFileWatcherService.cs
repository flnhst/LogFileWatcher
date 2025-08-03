namespace LogFileWatcher;

public interface IFileWatcherService
{
    void WatchFile(string fullPath, bool ignoreUntilEnd = false);

    void FileChanged(string fullPath);

    void RemoveFile(string fullPath);
}
