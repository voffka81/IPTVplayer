using TV_Player.Plugins;

namespace TV_Player.Providers;

/// <summary>Registry of trusted channel providers. Dynamic DLL loading is a separate milestone.</summary>
public sealed class ChannelProviderRegistry
{
    private readonly Dictionary<string, IChannelProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<IChannelProvider> Providers => _providers.Values.ToArray();

    public void Register(IChannelProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (string.IsNullOrWhiteSpace(provider.Id))
            throw new ArgumentException("Provider ID cannot be empty.", nameof(provider));
        if (!_providers.TryAdd(provider.Id, provider))
            throw new InvalidOperationException($"Provider '{provider.Id}' is already registered.");
    }

    public bool TryGet(string providerId, out IChannelProvider? provider) =>
        _providers.TryGetValue(providerId, out provider);
}
