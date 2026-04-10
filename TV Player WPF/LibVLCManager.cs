using LibVLCSharp.Shared;

namespace TV_Player
{
    public static class LibVLCManager
    {
        private static readonly object _lock = new object();
        private static LibVLC _instance;

        public static LibVLC Instance
        {
            get
            {
                if (_instance != null) return _instance;
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        Core.Initialize();
                        // Use conservative libvlc options to reduce native crashes
                        var options = new[]
                        {
                            "--ignore-config",
                            "--no-osd",
                            "--no-snapshot-preview",
                            "--avcodec-hw=none",
                            "--network-caching=3000",
                            "--http-reconnect",
                            "--rtsp-tcp"
                        };
                        _instance = new LibVLC(options);
                    }
                    return _instance;
                }
            }
        }

        public static void Dispose()
        {
            lock (_lock)
            {
                try { _instance?.Dispose(); } catch { }
                _instance = null;
            }
        }
    }
}
