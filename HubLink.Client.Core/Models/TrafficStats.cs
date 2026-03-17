using HubLink.Shared;

namespace HubLink.Client.Models
{
    public class TrafficStats : ITrafficStats
    {
        public long TotalBytesSent { get; private set; }
        public long TotalBytesReceived { get; private set; }
        public DateTime StartTime { get; private set; }
        public int ActiveConnections { get; set; }
        
        private long _lastBytesSent = 0;
        private long _lastBytesReceived = 0;
        private DateTime _lastUpdateTime = DateTime.Now;
        private double _uploadSpeed = 0;
        private double _downloadSpeed = 0;

        public TrafficStats()
        {
            StartTime = DateTime.Now;
        }

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
            TotalBytesSent = 0;
            TotalBytesReceived = 0;
            StartTime = DateTime.Now;
            _lastBytesSent = 0;
            _lastBytesReceived = 0;
            _lastUpdateTime = DateTime.Now;
            _uploadSpeed = 0;
            _downloadSpeed = 0;
        }

        public void UpdateSpeed()
        {
            var now = DateTime.Now;
            var elapsed = (now - _lastUpdateTime).TotalSeconds;
            
            if (elapsed >= 0.5)
            {
                var sentDelta = TotalBytesSent - _lastBytesSent;
                var receivedDelta = TotalBytesReceived - _lastBytesReceived;
                
                var newUploadSpeed = sentDelta / elapsed;
                var newDownloadSpeed = receivedDelta / elapsed;
                
                _uploadSpeed = newUploadSpeed;
                _downloadSpeed = newDownloadSpeed;
                
                _lastBytesSent = TotalBytesSent;
                _lastBytesReceived = TotalBytesReceived;
                _lastUpdateTime = now;
            }
            else if (elapsed >= 0.1)
            {
                _uploadSpeed *= 0.9;
                _downloadSpeed *= 0.9;
                
                if (_uploadSpeed < 0.1) _uploadSpeed = 0;
                if (_downloadSpeed < 0.1) _downloadSpeed = 0;
            }
        }

        public TimeSpan GetUptime()
        {
            return DateTime.Now - StartTime;
        }

        public double GetUploadSpeed()
        {
            UpdateSpeed();
            return _uploadSpeed;
        }

        public double GetDownloadSpeed()
        {
            UpdateSpeed();
            return _downloadSpeed;
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

        public override string ToString()
        {
            return $"上传: {FormatBytes(TotalBytesSent)} | 下载: {FormatBytes(TotalBytesReceived)} | 活跃连接: {ActiveConnections} | 运行时间: {GetUptime():hh\\:mm\\:ss}";
        }
    }
}