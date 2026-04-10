using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using LibVLCSharp.Shared;

class Command
{
    public string cmd { get; set; }
    public string url { get; set; }
}

class Response
{
    public string status { get; set; }
    public string message { get; set; }
}

class Program
{
    static LibVLC _libVlc;
    static MediaPlayer _player;
    static Media _media;

    static async Task<int> Main(string[] args)
    {
        Core.Initialize();
        // create libvlc with conservative options to reduce native crashes
        _libVlc = new LibVLC(new[] { "--ignore-config", "--no-osd", "--avcodec-hw=none", "--network-caching=3000", "--http-reconnect" });

        using var stdin = Console.OpenStandardInput();
        using var reader = new StreamReader(stdin);
        string line;
        // write banner
        Console.Out.WriteLine(JsonSerializer.Serialize(new Response { status = "ready", message = "VlcHost ready" }));
        Console.Out.Flush();

        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var cmd = JsonSerializer.Deserialize<Command>(line);
                if (cmd == null)
                {
                    WriteResponse("error", "invalid command");
                    continue;
                }

                switch (cmd.cmd?.ToLowerInvariant())
                {
                    case "play":
                        await CmdPlay(cmd.url);
                        break;
                    case "stop":
                        CmdStop();
                        break;
                    case "ping":
                        WriteResponse("ok", "pong");
                        break;
                    case "exit":
                        WriteResponse("ok", "exiting");
                        return 0;
                    default:
                        WriteResponse("error", "unknown command");
                        break;
                }
            }
            catch (Exception ex)
            {
                WriteResponse("error", ex.Message);
            }
        }

        return 0;
    }

    static async Task CmdPlay(string url)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                WriteResponse("error", "url empty");
                return;
            }

            CmdStop();

            _media = new Media(_libVlc, url, FromType.FromLocation);
            _player = new MediaPlayer(_libVlc);
            _player.Play(_media);

            WriteResponse("ok", "playing");
        }
        catch (Exception ex)
        {
            WriteResponse("error", ex.Message);
        }
    }

    static void CmdStop()
    {
        try
        {
            if (_player != null)
            {
                _player.Stop();
                try { _media?.Dispose(); } catch { }
                try { _player.Dispose(); } catch { }
                _player = null;
                _media = null;
            }
            WriteResponse("ok", "stopped");
        }
        catch (Exception ex)
        {
            WriteResponse("error", ex.Message);
        }
    }

    static void WriteResponse(string status, string message)
    {
        var resp = new Response { status = status, message = message };
        var json = JsonSerializer.Serialize(resp);
        Console.Out.WriteLine(json);
        Console.Out.Flush();
    }
}
