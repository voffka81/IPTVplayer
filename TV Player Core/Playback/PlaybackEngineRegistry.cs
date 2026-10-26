namespace TV_Player.Playback;

/// <summary>
/// Registers backend factories without eagerly loading native playback libraries.
/// A factory is invoked only when its backend is selected.
/// </summary>
public sealed class PlaybackEngineRegistry
{
    private readonly Dictionary<string, Func<IPlaybackEngine>> _factories =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> AvailableEngineIds => _factories.Keys.ToArray();

    public void Register(string id, Func<IPlaybackEngine> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(factory);

        if (!_factories.TryAdd(id, factory))
            throw new InvalidOperationException($"Playback engine '{id}' is already registered.");
    }

    public IPlaybackEngine Create(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!_factories.TryGetValue(id, out var factory))
            throw new KeyNotFoundException($"Playback engine '{id}' is not registered.");

        return factory() ?? throw new InvalidOperationException(
            $"Playback engine factory '{id}' returned null.");
    }
}
