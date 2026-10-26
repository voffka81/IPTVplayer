using System.Runtime.InteropServices;

namespace TV_Player.Playback;

/// <summary>
/// libmpv backend. Requires the platform libmpv native library at runtime.
/// The UI supplies a native child-window handle via SetVideoOutputHandle.
/// </summary>
public sealed class MpvPlaybackEngine : IPlaybackEngine, IVideoSurfacePlaybackEngine
{
    private nint _mpv;
    private nint _windowHandle;
    private bool _disposed;
    private PlaybackState _state = PlaybackState.Stopped;
    private int _volume = 100;

    public string Id => "mpv";
    public PlaybackState State => _state;

    public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;
    public event EventHandler<PlaybackErrorEventArgs>? ErrorOccurred;

    public int Volume
    {
        get => _volume;
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _volume = Math.Clamp(value, 0, 100);
            if (_mpv != 0)
                Check(MpvNative.mpv_set_property_string(_mpv, "volume", _volume.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
    }

    public void SetVideoOutputHandle(nint handle)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_mpv != 0)
            throw new InvalidOperationException("Video output handle must be set before initializing mpv.");
        if (handle == 0)
            throw new ArgumentException("A non-zero native window handle is required.", nameof(handle));
        _windowHandle = handle;
    }

    public Task PlayAsync(StreamSource source, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(source.Url);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            EnsureInitialized();
            SetState(PlaybackState.Opening);
            // mpv command arguments must be passed as an array of UTF-8 pointers.
            Command("loadfile", source.Url, "replace");
            SetState(PlaybackState.Playing); // Command accepted; playback readiness is not confirmed.
        }
        catch (Exception ex)
        {
            SetState(PlaybackState.Failed);
            ErrorOccurred?.Invoke(this, new PlaybackErrorEventArgs(ex));
            throw;
        }
        return Task.CompletedTask;
    }

    public Task PauseAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (_mpv != 0)
        {
            Check(MpvNative.mpv_set_property_string(_mpv, "pause", "yes"));
            SetState(PlaybackState.Paused);
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (_mpv != 0)
            Command("stop");
        SetState(PlaybackState.Stopped);
        return Task.CompletedTask;
    }

    private void EnsureInitialized()
    {
        if (_mpv != 0) return;
        if (_windowHandle == 0)
            throw new InvalidOperationException("SetVideoOutputHandle must be called before playback.");
        nint handle = MpvNative.mpv_create();
        if (handle == 0)
            throw new InvalidOperationException("mpv_create failed.");
        try
        {
            Check(MpvNative.mpv_set_option_string(handle, "wid", _windowHandle.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            Check(MpvNative.mpv_set_option_string(handle, "terminal", "no"));
            Check(MpvNative.mpv_initialize(handle));
            Check(MpvNative.mpv_set_property_string(handle, "volume", _volume.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            _mpv = handle;
        }
        catch
        {
            MpvNative.mpv_terminate_destroy(handle);
            throw;
        }
    }

    private void Command(params string[] args)
    {
        nint array = Marshal.AllocHGlobal((args.Length + 1) * IntPtr.Size);
        var pointers = new nint[args.Length];
        try
        {
            for (int i = 0; i < args.Length; i++)
            {
                pointers[i] = Marshal.StringToCoTaskMemUTF8(args[i]);
                Marshal.WriteIntPtr(array, i * IntPtr.Size, pointers[i]);
            }
            Marshal.WriteIntPtr(array, args.Length * IntPtr.Size, 0);
            Check(MpvNative.mpv_command(_mpv, array));
        }
        finally
        {
            foreach (var ptr in pointers)
                if (ptr != 0) Marshal.FreeCoTaskMem(ptr);
            Marshal.FreeHGlobal(array);
        }
    }

    private static void Check(int error)
    {
        if (error < 0)
            throw new InvalidOperationException($"libmpv returned error {error}.");
    }

    private void SetState(PlaybackState next)
    {
        if (_state == next) return;
        var previous = _state;
        _state = next;
        StateChanged?.Invoke(this, new PlaybackStateChangedEventArgs(previous, next));
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        if (_mpv != 0)
        {
            MpvNative.mpv_terminate_destroy(_mpv);
            _mpv = 0;
        }
        SetState(PlaybackState.Stopped);
        return ValueTask.CompletedTask;
    }

    private static class MpvNative
    {
        [DllImport("mpv", CallingConvention = CallingConvention.Cdecl)]
        internal static extern nint mpv_create();

        [DllImport("mpv", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int mpv_initialize(nint ctx);

        [DllImport("mpv", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int mpv_set_option_string(nint ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);

        [DllImport("mpv", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int mpv_set_property_string(nint ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);

        [DllImport("mpv", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int mpv_command(nint ctx, nint args);

        [DllImport("mpv", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void mpv_terminate_destroy(nint ctx);
    }
}
