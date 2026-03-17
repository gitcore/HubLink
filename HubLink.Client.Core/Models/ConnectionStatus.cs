namespace HubLink.Client.Models
{
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Disconnecting,
        Reconnecting,
        Error
    }

    public class ConnectionStatus
    {
        public ConnectionState State { get; private set; }
        public DateTime? LastConnectedTime { get; private set; }
        public DateTime LastDisconnectedTime { get; private set; } = DateTime.Now;
        public string? ErrorMessage { get; private set; }
        public int ReconnectAttempts { get; private set; }
        private bool _isReconnecting = false;

        public event EventHandler<ConnectionStateChangedEventArgs>? StateChanged;

        public ConnectionStatus()
        {
            State = ConnectionState.Disconnected;
        }

        public void SetState(ConnectionState newState, string? errorMessage = null)
        {
            var oldState = State;
            State = newState;
            ErrorMessage = errorMessage;

            switch (newState)
            {
                case ConnectionState.Connected:
                    if (!_isReconnecting)
                    {
                        LastConnectedTime = DateTime.Now;
                    }
                    _isReconnecting = false;
                    ReconnectAttempts = 0;
                    break;
                case ConnectionState.Disconnected:
                case ConnectionState.Error:
                    LastDisconnectedTime = DateTime.Now;
                    _isReconnecting = false;
                    break;
                case ConnectionState.Disconnecting:
                    break;
                case ConnectionState.Reconnecting:
                    _isReconnecting = true;
                    ReconnectAttempts++;
                    break;
            }

            StateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(oldState, newState, errorMessage));
        }

        public TimeSpan GetConnectionDuration()
        {
            if (State == ConnectionState.Connected && LastConnectedTime.HasValue)
            {
                return DateTime.Now - LastConnectedTime.Value;
            }
            return TimeSpan.Zero;
        }

        public string GetStateString()
        {
            return State switch
            {
                ConnectionState.Disconnected => "DISCONNECTED",
                ConnectionState.Connecting => "CONNECTING",
                ConnectionState.Connected => "CONNECTED",
                ConnectionState.Disconnecting => "DISCONNECTING",
                ConnectionState.Reconnecting => $"RECONNECTING ({ReconnectAttempts})",
                ConnectionState.Error => $"ERROR: {ErrorMessage}",
                _ => "UNKNOWN"
            };
        }
    }

    public class ConnectionStateChangedEventArgs : EventArgs
    {
        public ConnectionState OldState { get; }
        public ConnectionState NewState { get; }
        public string? ErrorMessage { get; }

        public ConnectionStateChangedEventArgs(ConnectionState oldState, ConnectionState newState, string? errorMessage)
        {
            OldState = oldState;
            NewState = newState;
            ErrorMessage = errorMessage;
        }
    }
}