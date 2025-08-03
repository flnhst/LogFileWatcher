namespace LogFileWatcher;

public interface IFileWatcherService
{
    void WatchFile(string fullPath);

    void FileChanged(string fullPath);

    void RemoveFile(string fullPath);
}
