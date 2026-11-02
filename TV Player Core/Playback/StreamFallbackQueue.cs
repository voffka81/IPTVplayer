namespace TV_Player.Playback;

/// <summary>Tracks alternate stream URLs for the current channel independently of the player UI.</summary>
public sealed class StreamFallbackQueue
{
    private IReadOnlyList<string> _urls = Array.Empty<string>();
    public int Attempted { get; private set; }
    public int Total => _urls.Count;
    public bool HasNext => Attempted < Total;

    public void Reset(IReadOnlyList<string>? urls)
    {
        _urls = urls?.ToArray() ?? Array.Empty<string>();
        Attempted = 0;
    }

    public bool TryNext(out string url)
    {
        if (!HasNext)
        {
            url = string.Empty;
            return false;
        }

        url = _urls[Attempted++];
        return true;
    }
}
