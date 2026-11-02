using LibVLCSharp.Shared;

namespace TV_Player.AvaloniaApp.Services;

internal static class LibVlcRuntime
{
    private static readonly Lazy<Task<LibVLC>> Initialization = new(() => Task.Run(() =>
    {
        Core.Initialize();
        return new LibVLC(enableDebugLogs: true);
    }));

    public static Task<LibVLC> InitializeAsync() => Initialization.Value;

    public static async Task WarmupAsync()
    {
        try
        {
            await InitializeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LibVLC background initialization failed: {ex}");
        }
    }

    public static void Dispose()
    {
        if (!Initialization.IsValueCreated)
            return;

        var initialization = Initialization.Value;
        if (initialization.IsCompletedSuccessfully)
            initialization.Result.Dispose();
        else
            _ = initialization.ContinueWith(task =>
            {
                if (task.Status == TaskStatus.RanToCompletion)
                    task.Result.Dispose();
            }, TaskScheduler.Default);
    }
}
