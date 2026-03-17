namespace HubLink.Server.Services;

public class ServerTrafficStats : ITrafficStats
{
    public long TotalBytesSent { get; private set; }
    public long TotalBytesReceived { get; private set; }
    public int ActiveConnections { get; private set; }

    public void SetActiveConnections(int count)
    {
        ActiveConnections = count;
    }

    public void AddSent(long bytes)
    {
        TotalBytesSent += bytes;
    }

    public void AddReceived(long bytes)
    {
        TotalBytesReceived += bytes;
    }

    public void Reset()
    {
    }

    public void UpdateSpeed()
    {
    }

    public TimeSpan GetUptime()
    {
        return TimeSpan.Zero;
    }

    public double GetUploadSpeed()
    {
        return 0;
    }

    public double GetDownloadSpeed()
    {
        return 0;
    }

    public string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    public string FormatSpeed(double bytesPerSecond)
    {
        return $"{FormatBytes((long)bytesPerSecond)}/s";
    }
}