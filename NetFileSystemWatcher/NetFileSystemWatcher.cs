using System;
using System.ComponentModel;
using System.IO;
using System.Timers;

namespace TheLostDotNETChronicles.FileSystemWatchers
{
    public enum ConnectionState
    {
        /// <summary>
        /// Connection to filesystem is OK
        /// </summary>
        Connected,
        /// <summary>
        /// Connection to filesystem has failed.
        /// </summary>
        Disconnected,
        /// <summary>
        /// Pseudo state to indicate that file watcher has just been reconnected. 
        /// File watcher will never be in this state.
        /// </summary>
        Reconnected
    }

    public class ConnectionStateChangedEventArgs : EventArgs
    {
        public ConnectionStateChangedEventArgs(ConnectionState connectionState)
        {
            ConnectionState = connectionState;
        }

        public ConnectionState ConnectionState { get; private set; }
    }

    /// <summary>
    /// Listens to the file system change notifications and raises events when a network directory, or file in a network directory, changes.
    /// Use FileSystemWatcher for monitoring non network directory.
    /// </summary>
    [System.ComponentModel.DesignerCategory("Code")] // Prevent VS2010 from viewing as a component
    public class NetFileSystemWatcher : FileSystemWatcher
    {
        const int DefaultCheckConnectionIntervalSeconds = 60 * 30; // Default 30 minutes between 

        private readonly object _lock = new object();
        private Timer _timer;

        /// <summary>
        /// Initializes a new instance of the NetFileSystemWatcher class.
        /// </summary>
        public NetFileSystemWatcher()
        {
            Init();
        }

        /// <summary>
        /// Initializes a new instance of the NetFileSystemWatcher class, given the specified directory to monitor.
        /// </summary>
        /// <param name="path">The directory to monitor, in standard or Universal Naming Convention (UNC) notation.</param>
        public NetFileSystemWatcher(string path)
            : base(path)
        {
            Init();
        }

        /// <summary>
        ///  Initializes a new instance of the NetFileSystemWatcher class, given the specified directory and type of files to monitor.
        /// </summary>
        /// <param name="path">The directory to monitor, in standard or Universal Naming Convention (UNC) notation.</param>
        /// <param name="filter">The type of files to watch. For example, "*.txt" watches for changes to all text files.</param>
        public NetFileSystemWatcher(string path, string filter)
            : base(path, filter)
        {
            Init();
        }

        /// <summary>
        /// Initializes a new instance of the NetFileSystemWatcher.
        /// </summary>
        /// <param name="checkConnectionInterval">When there has been no activity for this period of time, the connection will be checked.</param>
        /// <param name="path">The directory to monitor, in standard or Universal Naming Convention (UNC) notation.</param>
        /// <param name="filter">The type of files to watch. For example, "*.txt" watches for changes to all text files.</param>
        public NetFileSystemWatcher(TimeSpan checkConnectionInterval)
        {
            Init(checkConnectionInterval);
        }

        /// <summary>
        /// Initializes a new instance of the NetFileSystemWatcher, given the specified directory.
        /// </summary>
        /// <param name="checkConnectionInterval">When there has been no activity for this period of time, the connection will be checked.</param>
        /// <param name="path">The directory to monitor, in standard or Universal Naming Convention (UNC) notation.</param>
        /// <param name="filter">The type of files to watch. For example, "*.txt" watches for changes to all text files.</param>
        public NetFileSystemWatcher(TimeSpan checkConnectionInterval, string path)
            : base(path)
        {
            Init(checkConnectionInterval);
        }

        /// <summary>
        /// Initializes a new instance of the NetFileSystemWatcher, given the specified directory and type of files to monitor.
        /// </summary>
        /// <param name="checkConnectionInterval">When there has been no activity for this period of time, the connection will be checked.</param>
        /// <param name="path">The directory to monitor, in standard or Universal Naming Convention (UNC) notation.</param>
        /// <param name="filter">The type of files to watch. For example, "*.txt" watches for changes to all text files.</param>
        public NetFileSystemWatcher(TimeSpan checkConnectionInterval, string path, string filter)
            : base(path, filter)
        {
            Init(checkConnectionInterval);
        }

        private void Init()
        {
            Init(TimeSpan.FromSeconds(DefaultCheckConnectionIntervalSeconds));
        }

        private TimeSpan _checkConnectionInterval;
        private TimeSpan CheckConnectionInterval
        {
            get { return _checkConnectionInterval; }
            set
            {
                // Sanity check
                if (value.TotalMilliseconds < 1000)
                    throw new ArgumentException("Value too low", "checkConnectionInterval");
                _checkConnectionInterval = value;
            }
        }

        private void Init(TimeSpan checkConnectionInterval)
        {
            CheckConnectionInterval = checkConnectionInterval;
            ConnectionState = ConnectionState.Connected;

            Error += HandleErrors;
            this.Changed += ResetTimer;
            this.Deleted += ResetTimer;
            this.Created += ResetTimer;
            this.Renamed += ResetTimer;

            _timer = new Timer(GetTimerInterval(ConnectionState));
            _timer.Elapsed += ConnectCheckTimerElapsed;
            _timer.Enabled = true;
        }

        private void ResetTimer(object sender, FileSystemEventArgs e)
        {
            // Reset timer
            _timer.Enabled = false;
            _timer.Enabled = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (_timer != null)
            {
                Error -= HandleErrors;
                this.Changed -= ResetTimer;
                this.Deleted -= ResetTimer;
                this.Created -= ResetTimer;
                this.Renamed -= ResetTimer;
                _timer.Enabled = false;
                _timer.Elapsed -= ConnectCheckTimerElapsed;
                _timer = null;
            }

            base.Dispose(disposing);
        }

        private void ConnectCheckTimerElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            CheckConnection();
        }

        private void HandleErrors(object sender, ErrorEventArgs e)
        {
            SetConnectionState(ConnectionState.Disconnected);
        }

        private void CheckConnection()
        {
            if (EnableRaisingEvents || ConnectionState == ConnectionState.Disconnected)
            {
                _timer.Enabled = false;
                try
                {
                    EnableRaisingEvents = false;
                    EnableRaisingEvents = true;

                    SetConnectionState(ConnectionState.Connected);
                }
                catch
                {
                    SetConnectionState(ConnectionState.Disconnected);
                }
                _timer.Interval = GetTimerInterval(ConnectionState);
                _timer.Enabled = true;
            }
        }

        private void SetConnectionState(ConnectionState newState)
        {
            if (ConnectionState != newState)
            {
                bool stateChanged = false;

                lock (_lock)
                {
                    if (ConnectionState != newState)
                    {
                        InStateCounter = 0;
                        ConnectionState = newState;
                        stateChanged = true;
                    }
                }

                if (stateChanged)
                    OnConnecionStateChanged(new ConnectionStateChangedEventArgs(newState));
            }
            else
            {
                InStateCounter++;
                if (newState == ConnectionState.Connected)
                    OnConnecionStateChanged(new ConnectionStateChangedEventArgs(ConnectionState.Reconnected));
            }
        }

        /// <summary>
        /// Calculate how long to wait for next connection check
        /// </summary>
        /// <param name="newState"></param>
        /// <returns></returns>
        private double GetTimerInterval(ConnectionState newState)
        {
            switch(newState)
            {
                case ConnectionState.Connected:
                    // When connected simply use CheckConnectionInterval as 
                    return CheckConnectionInterval.TotalMilliseconds;
                default:
                    // When disconnected use InStateCounter as seconds as timer (but never higher than CheckConnectionInterval)
                    return Math.Min(
                        TimeSpan.FromSeconds(InStateCounter+1).TotalMilliseconds, 
                        CheckConnectionInterval.TotalMilliseconds
                        );
            }
        }

        /// <summary>
        /// Returns the current state of the FileSystemWatcher
        /// </summary>
        public ConnectionState ConnectionState { get; private set; }

        /// <summary>
        /// Tracks the number of times the timer has elapsed in current state
        /// </summary>
        protected long InStateCounter { get; private set; }

        /// <summary>
        /// Occurs when connection to the network folder is lost or reconnected.
        /// </summary>
        [IODescription("FSW_ConnectionStateChanged")]
        public event EventHandler<ConnectionStateChangedEventArgs> ConnectionStateChanged;

        /// <summary>
        /// Raises the NetFileSystemWatcher.ConnectionStateChanged event.
        /// </summary>
        /// <param name="eventArgs">A ConnectionStateChangedEventArgs that contain the event data.</param>
        protected virtual void OnConnecionStateChanged(ConnectionStateChangedEventArgs eventArgs)
        {
            // Copy to temp to be thread safe.
            var temp = ConnectionStateChanged;
            if (temp != null)
                temp(this, eventArgs);
        }
    }
}