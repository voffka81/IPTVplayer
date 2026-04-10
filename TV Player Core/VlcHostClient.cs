using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TV_Player
{
    // Simple supervisor client to talk to an external VlcHost process via stdio lines (JSON per line).
    // This is a minimal implementation: it starts the helper if present and provides async SendCommand/GetResponse.
    public class VlcHostClient : IDisposable
    {
        private Process _process;
        private StreamWriter _writer;
        private StreamReader _reader;
        private readonly object _lock = new object();
        private readonly string _exePath;
        private CancellationTokenSource _restartCts;

        public bool IsRunning => _process != null && !_process.HasExited;

        public VlcHostClient(string exePath = null)
        {
            _exePath = exePath ?? Path.Combine(AppContext.BaseDirectory ?? string.Empty, "VlcHost.exe");
        }

        public bool EnsureStarted()
        {
            lock (_lock)
            {
                if (IsRunning) return true;
                if (!File.Exists(_exePath)) return false;

                StartProcess();
                return IsRunning;
            }
        }

        private void StartProcess()
        {
            var psi = new ProcessStartInfo(_exePath)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };

            _process = Process.Start(psi);
            if (_process == null) return;

            _process.EnableRaisingEvents = true;
            _process.Exited += Process_Exited;

            _writer = new StreamWriter(_process.StandardInput.BaseStream, Encoding.UTF8) { AutoFlush = true };
            _reader = new StreamReader(_process.StandardOutput.BaseStream, Encoding.UTF8);

            // optionally read initial banner asynchronously
            Task.Run(async () =>
            {
                try
                {
                    for (int i = 0; i < 5; i++)
                    {
                        if (_reader.Peek() >= 0)
                        {
                            var line = await _reader.ReadLineAsync().ConfigureAwait(false);
                            Debug.WriteLine($"[VlcHostClient] banner: {line}");
                        }
                        else break;
                    }
                }
                catch { }
            });
        }

        private void Process_Exited(object sender, EventArgs e)
        {
            Debug.WriteLine("[VlcHostClient] helper process exited, scheduling restart");
            lock (_lock)
            {
                try { _writer?.Dispose(); } catch { }
                try { _reader?.Dispose(); } catch { }
                _writer = null;
                _reader = null;
            }

            // start a restart loop
            _restartCts?.Cancel();
            _restartCts = new CancellationTokenSource();
            var token = _restartCts.Token;
            Task.Run(async () =>
            {
                int attempt = 0;
                while (!token.IsCancellationRequested && attempt < 20)
                {
                    attempt++;
                    Debug.WriteLine($"[VlcHostClient] restart attempt {attempt}");
                    try
                    {
                        if (File.Exists(_exePath))
                        {
                            StartProcess();
                            if (IsRunning)
                            {
                                Debug.WriteLine("[VlcHostClient] helper restarted");
                                return;
                            }
                        }
                    }
                    catch { }

                    await Task.Delay(TimeSpan.FromSeconds(2), token).ContinueWith(_ => { });
                }
                Debug.WriteLine("[VlcHostClient] restart attempts exhausted");
            }, token);
        }

        public async Task<string> SendCommandAsync(object command, int timeoutMs = 2000)
        {
            if (!EnsureStarted()) return null;
            var json = JsonSerializer.Serialize(command);
            try
            {
                await _writer.WriteLineAsync(json).ConfigureAwait(false);
            }
            catch
            {
                return null;
            }

            using var cts = new CancellationTokenSource(timeoutMs);
            try
            {
                var t = _reader.ReadLineAsync();
                var completed = await Task.WhenAny(t, Task.Delay(timeoutMs, cts.Token)).ConfigureAwait(false);
                if (completed == t)
                {
                    return await t.ConfigureAwait(false);
                }
            }
            catch { }
            return null;
        }

        public Task<string> PlayAsync(string url) => SendCommandAsync(new { cmd = "play", url });
        public Task<string> StopAsync() => SendCommandAsync(new { cmd = "stop" });
        public Task<string> PingAsync() => SendCommandAsync(new { cmd = "ping" });

        public void Dispose()
        {
            try
            {
                _restartCts?.Cancel();
            }
            catch { }

            try
            {
                if (IsRunning)
                {
                    try { _writer?.WriteLine("{\"cmd\":\"exit\"}"); } catch { }
                    try { _process?.Kill(); } catch { }
                }
            }
            catch { }
            try { _writer?.Dispose(); } catch { }
            try { _reader?.Dispose(); } catch { }
            try { _process?.Dispose(); } catch { }
            _process = null;
        }
    }
}
