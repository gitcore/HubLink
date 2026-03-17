namespace HubLink.Shared;

public interface ITrafficStats
{
    long TotalBytesSent { get; }
    long TotalBytesReceived { get; }
    int ActiveConnections { get; }
    void SetActiveConnections(int count);
    void AddSent(long bytes);
    void AddReceived(long bytes);
    void Reset();
    void UpdateSpeed();
    TimeSpan GetUptime();
    double GetUploadSpeed();
    double GetDownloadSpeed();
    string FormatBytes(long bytes);
    string FormatSpeed(double bytesPerSecond);
}